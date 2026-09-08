using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>
    /// CPOE orders — one generic handler pair for every OrderType (Medication/Lab/Radiology/
    /// Procedure/Diet/Nursing). Placing an order posts a BillingChargeEvent per chargeable line
    /// immediately (charge-on-event) by calling into AddChargeEventHandler via MediatR — this
    /// intentionally reuses the existing GST/discount/incentive engine instead of duplicating it.
    /// Both handlers run inside a transaction so a billing failure rolls back the clinical order too.
    /// </summary>
    public class ClinicalOrderCommandHandlers :
        IRequestHandler<PlaceClinicalOrderRequestModel, PlaceClinicalOrderResponseModel>,
        IRequestHandler<DiscontinueClinicalOrderLineRequestModel, DiscontinueClinicalOrderLineResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IMediator _mediator;

        public ClinicalOrderCommandHandlers(AppDbContext context, IMediator mediator)
        {
            _context = context;
            _mediator = mediator;
        }

        public async Task<PlaceClinicalOrderResponseModel> Handle(PlaceClinicalOrderRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                    return new PlaceClinicalOrderResponseModel { Success = false, Message = "HospitalId and AdmissionId are required." };
                var orderType = request.OrderType?.Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(orderType) || !IpdConstants.ClinicalOrderType.All.Contains(orderType))
                    return new PlaceClinicalOrderResponseModel { Success = false, Message = "Invalid order type." };
                if (request.Lines == null || request.Lines.Count == 0)
                    return new PlaceClinicalOrderResponseModel { Success = false, Message = "At least one line is required." };
                if (request.Lines.Any(l => string.IsNullOrWhiteSpace(l.ItemName)))
                    return new PlaceClinicalOrderResponseModel { Success = false, Message = "Each line requires an item name." };

                var admission = await _context.Admission
                    .FirstOrDefaultAsync(a => a.AdmissionId == request.AdmissionId && a.HospitalId == request.HospitalId, cancellationToken);
                if (admission == null)
                    return new PlaceClinicalOrderResponseModel { Success = false, Message = "Admission not found." };
                if (!IpdConstants.AdmissionStatus.Active.Contains(admission.StatusCode))
                    return new PlaceClinicalOrderResponseModel { Success = false, Message = "Admission is not active." };

                var strategy = _context.Database.CreateExecutionStrategy();
                return await strategy.ExecuteAsync(async () =>
                {
                    await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
                    try
                    {
                        var now = DateTime.UtcNow;
                        var order = new ClinicalOrder
                        {
                            OrderId = Guid.NewGuid(),
                            HospitalId = request.HospitalId,
                            AdmissionId = admission.AdmissionId,
                            EncounterId = admission.EncounterId,
                            PatientId = admission.PatientId,
                            OrderType = orderType,
                            StatusCode = IpdConstants.ClinicalOrderStatus.Active,
                            OrderedAt = now,
                            OrderedBy = request.LoggedInUserName,
                            OrderedByDoctorId = request.OrderedByDoctorId,
                            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                            SurgeryCaseId = request.SurgeryCaseId,
                            SourceOrderSetId = request.SourceOrderSetId,
                            SourceOrderSetNameSnapshot = string.IsNullOrWhiteSpace(request.SourceOrderSetNameSnapshot) ? null : request.SourceOrderSetNameSnapshot.Trim(),
                            CreatedAt = now,
                            CreatedBy = request.LoggedInUserName,
                            UpdatedAt = now,
                            UpdatedBy = request.LoggedInUserName,
                        };
                        _context.ClinicalOrder.Add(order);

                        var lines = new List<ClinicalOrderLine>();
                        for (int i = 0; i < request.Lines.Count; i++)
                        {
                            var li = request.Lines[i];
                            var line = new ClinicalOrderLine
                            {
                                OrderLineId = Guid.NewGuid(),
                                OrderId = order.OrderId,
                                HospitalId = request.HospitalId,
                                ChargeId = li.ChargeId,
                                DisplayOrder = i,
                                ItemName = li.ItemName.Trim(),
                                SaltName = li.SaltName?.Trim(),
                                Dose = li.Dose?.Trim(),
                                Route = li.Route?.Trim(),
                                Frequency = li.Frequency?.Trim(),
                                DurationDays = li.DurationDays,
                                Instructions = li.Instructions?.Trim(),
                                Urgency = string.IsNullOrWhiteSpace(li.Urgency) ? null : li.Urgency.Trim().ToUpperInvariant(),
                                ScheduledAt = li.ScheduledAt,
                                IsHighAlert = li.IsHighAlert,
                                IsDailyRecurringCharge = li.IsDailyRecurringCharge,
                                Qty = li.Qty <= 0 ? 1 : li.Qty,
                                StatusCode = IpdConstants.ClinicalOrderLineStatus.Active,
                                CreatedAt = now,
                                CreatedBy = request.LoggedInUserName,
                                UpdatedAt = now,
                                UpdatedBy = request.LoggedInUserName,
                            };
                            _context.ClinicalOrderLine.Add(line);
                            lines.Add(line);
                        }

                        // ── Charge-on-event: post one BillingChargeEvent per chargeable line ────
                        // Daily-recurring lines (oxygen, continuous monitoring) are excluded here —
                        // they accrue once per IST day via the nightly PostDailyRecurringCharges job
                        // instead of being charged once at order time.
                        // anyChargesPosted drives a best-effort draft-invoice creation after commit
                        // below — AddChargeEventHandler alone never creates a BillingInvoice, and
                        // without one the charge is real but invisible on both the Billing Dashboard
                        // and Pathology's own Billing tab until someone separately invoices the
                        // encounter (same gap already fixed for the Pathology module's own order
                        // path — see PathologyAutoBillingHelper.PostChargesAndInvoiceAsync).
                        bool anyChargesPosted = false;
                        if (admission.EncounterId.HasValue)
                        {
                            var chargeableIndices = Enumerable.Range(0, lines.Count).Where(i => lines[i].ChargeId.HasValue && !lines[i].IsDailyRecurringCharge).ToList();
                            if (chargeableIndices.Count > 0)
                            {
                                var chargeIds = chargeableIndices.Select(i => lines[i].ChargeId!.Value).Distinct().ToList();
                                var masters = await _context.ChargeMaster
                                    .Where(m => m.HospitalId == request.HospitalId && chargeIds.Contains(m.ChargeId))
                                    .ToDictionaryAsync(m => m.ChargeId, cancellationToken);

                                var chargeDetails = chargeableIndices.Select(i =>
                                {
                                    var line = lines[i];
                                    masters.TryGetValue(line.ChargeId!.Value, out var master);
                                    return new ChargeDetail
                                    {
                                        ChargeId = line.ChargeId,
                                        DisplayName = master?.DisplayName ?? line.ItemName,
                                        Qty = line.Qty,
                                        Rate = master?.DefaultRate ?? 0,
                                        DiscountPercent = 0,
                                        CategoryCode = master?.CategoryCode ?? DefaultCategoryFor(orderType),
                                        // Every Lab-type CPOE line bills as pathology-sourced regardless of the
                                        // ChargeMaster row's own category, so a downstream "which charges came
                                        // from the lab" filter (Billing Ledger badges, Pathology's own billing
                                        // strip) can rely on SourceModule alone instead of guessing from CategoryCode.
                                        SourceModule = orderType == IpdConstants.ClinicalOrderType.Lab
                                            ? BillingConstants.SourceModule.LabPath
                                            : null,
                                        AttributedDoctorId = order.OrderedByDoctorId,
                                    };
                                }).ToList();

                                var chargeResponse = await _mediator.Send(new AddChargeEventRequestModel
                                {
                                    HospitalId = request.HospitalId,
                                    PatientId = admission.PatientId,
                                    EncounterId = admission.EncounterId.Value,
                                    Charges = chargeDetails,
                                    LoggedInUserName = request.LoggedInUserName,
                                    LoggedInUserId = request.LoggedInUserId,
                                }, cancellationToken);

                                if (chargeResponse.Success != true || chargeResponse.Data?.ChargeEvents == null)
                                {
                                    await tx.RollbackAsync(cancellationToken);
                                    return new PlaceClinicalOrderResponseModel
                                    {
                                        Success = false,
                                        Message = chargeResponse.Message ?? "Could not post charges for this order.",
                                    };
                                }

                                for (int k = 0; k < chargeableIndices.Count; k++)
                                    lines[chargeableIndices[k]].ChargeEventId = chargeResponse.Data.ChargeEvents[k].ChargeEventId;

                                anyChargesPosted = true;
                            }
                        }

                        // ── Lab orders also get a linked PathologyOrder, so they surface in the
                        // Pathology Lab workspace's structured results/report pipeline instead of
                        // staying invisible to it. Billing already happened above via the generic
                        // ChargeMaster charge-on-event path -- this never bills again, it only maps
                        // chargeable lines onto PathologyTestMaster (matched by ChargeId) to build
                        // the structured order. Lines with no ChargeId, or a ChargeId that doesn't
                        // resolve to a catalogued test, are left as plain ClinicalOrderLine entries.
                        if (orderType == IpdConstants.ClinicalOrderType.Lab)
                        {
                            var chargeIds = lines.Where(l => l.ChargeId.HasValue).Select(l => l.ChargeId!.Value).Distinct().ToList();
                            var matchedTests = chargeIds.Count == 0
                                ? new List<PathologyTestMaster>()
                                : await _context.PathologyTestMaster
                                    .Where(t => t.HospitalId == request.HospitalId && t.IsActive && t.ChargeId.HasValue && chargeIds.Contains(t.ChargeId.Value))
                                    .ToListAsync(cancellationToken);

                            if (matchedTests.Count > 0)
                            {
                                var testByChargeId = matchedTests.ToDictionary(t => t.ChargeId!.Value, t => t);
                                var pathologyLines = lines.Where(l => l.ChargeId.HasValue && testByChargeId.ContainsKey(l.ChargeId.Value)).ToList();

                                if (pathologyLines.Count > 0)
                                {
                                    string pathOrderNo = string.Empty;
                                    var pathNow = DateTime.UtcNow;
                                    for (int attempt = 0; attempt < 5; attempt++)
                                    {
                                        try
                                        {
                                            var numberSeries = await NumberSeriesDefaults.GetOrCreateAsync(
                                                _context, request.HospitalId, BillingConstants.NumberSeriesCode.LabAccession, request.LoggedInUserName, cancellationToken);
                                            numberSeries.CurrentValue++;
                                            pathOrderNo = NumberSeriesFormatter.Format(
                                                numberSeries.Prefix, numberSeries.YearFormat, numberSeries.Separator, numberSeries.PadLength, numberSeries.CurrentValue);
                                            numberSeries.UpdatedAt = pathNow;
                                            numberSeries.UpdatedBy = request.LoggedInUserName;
                                            break;
                                        }
                                        catch (DbUpdateException)
                                        {
                                            _context.ChangeTracker.Clear();
                                            if (attempt == 4) throw;
                                        }
                                    }

                                    var pathOrder = new PathologyOrder
                                    {
                                        OrderId = Guid.NewGuid(),
                                        HospitalId = request.HospitalId,
                                        PatientId = admission.PatientId,
                                        EncounterId = admission.EncounterId,
                                        AdmissionId = admission.AdmissionId,
                                        OrderedByDoctorId = order.OrderedByDoctorId,
                                        Notes = order.Notes,
                                        OrderNo = pathOrderNo,
                                        OrderDate = pathNow,
                                        Status = "PLACED",
                                        SourceType = "IPD",
                                        IsStat = pathologyLines.Any(l => string.Equals(l.Urgency, "STAT", StringComparison.OrdinalIgnoreCase)),
                                        CreatedAt = pathNow,
                                        CreatedBy = request.LoggedInUserName,
                                        UpdatedAt = pathNow,
                                        UpdatedBy = request.LoggedInUserName,
                                    };
                                    _context.PathologyOrder.Add(pathOrder);

                                    foreach (var clinicalLine in pathologyLines)
                                    {
                                        var test = testByChargeId[clinicalLine.ChargeId!.Value];
                                        var pathLine = new PathologyOrderLine
                                        {
                                            OrderLineId = Guid.NewGuid(),
                                            HospitalId = request.HospitalId,
                                            OrderId = pathOrder.OrderId,
                                            TestId = test.TestId,
                                            Status = "PENDING",
                                            CreatedAt = pathNow,
                                            CreatedBy = request.LoggedInUserName,
                                            UpdatedAt = pathNow,
                                            UpdatedBy = request.LoggedInUserName,
                                        };
                                        _context.PathologyOrderLine.Add(pathLine);
                                        clinicalLine.LinkedPathologyOrderLineId = pathLine.OrderLineId;
                                    }
                                }
                            }
                        }

                        await _context.SaveChangesAsync(cancellationToken);
                        await tx.CommitAsync(cancellationToken);

                        // Best-effort, run only after the order's own transaction has committed —
                        // CreateDraftInvoiceHandler manages its own execution-strategy/transaction,
                        // which can't nest inside the one just committed above. The order itself must
                        // not be undone by an invoicing hiccup (same "commit first, bill best-effort
                        // after" shape CollectPathologySampleHandler already uses for ON_SAMPLE_
                        // COLLECTION billing), so failures here are swallowed, not surfaced as a
                        // order-placement failure.
                        if (anyChargesPosted && admission.EncounterId.HasValue)
                        {
                            try
                            {
                                await _mediator.Send(new CreateDraftInvoiceRequestModel
                                {
                                    HospitalId = request.HospitalId,
                                    PatientId = admission.PatientId,
                                    EncounterId = admission.EncounterId.Value,
                                    LoggedInUserId = request.LoggedInUserId,
                                    LoggedInUserName = request.LoggedInUserName,
                                }, cancellationToken);
                            }
                            catch
                            {
                                // Swallow -- the clinical order and its charges already committed
                                // successfully; the encounter can still be invoiced manually from
                                // the Billing tab if this best-effort call didn't get there.
                            }
                        }

                        return new PlaceClinicalOrderResponseModel
                        {
                            Success = true,
                            Message = "Order placed.",
                            OrderId = order.OrderId,
                            LineCount = lines.Count,
                            ChargedLineCount = lines.Count(l => l.ChargeEventId.HasValue),
                        };
                    }
                    catch (Exception)
                    {
                        await tx.RollbackAsync(cancellationToken);
                        return new PlaceClinicalOrderResponseModel { Success = false, Message = "Error placing order." };
                    }
                });
            }
            catch (Exception)
            {
                return new PlaceClinicalOrderResponseModel { Success = false, Message = "Error placing order." };
            }
        }

        public async Task<DiscontinueClinicalOrderLineResponseModel> Handle(DiscontinueClinicalOrderLineRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.OrderLineId == Guid.Empty)
                    return new DiscontinueClinicalOrderLineResponseModel { Success = false, Message = "HospitalId and OrderLineId are required." };

                var line = await _context.ClinicalOrderLine
                    .FirstOrDefaultAsync(l => l.OrderLineId == request.OrderLineId && l.HospitalId == request.HospitalId, cancellationToken);
                if (line == null)
                    return new DiscontinueClinicalOrderLineResponseModel { Success = false, Message = "Order line not found." };
                if (line.StatusCode == IpdConstants.ClinicalOrderLineStatus.Discontinued)
                    return new DiscontinueClinicalOrderLineResponseModel { Success = false, Message = "Line is already discontinued." };

                var now = DateTime.UtcNow;
                line.StatusCode = IpdConstants.ClinicalOrderLineStatus.Discontinued;
                line.UpdatedAt = now;
                line.UpdatedBy = request.LoggedInUserName;

                var chargeVoided = false;
                if (line.ChargeEventId.HasValue)
                {
                    var chargeEvent = await _context.BillingChargeEvent
                        .FirstOrDefaultAsync(c => c.ChargeEventId == line.ChargeEventId.Value && c.StatusCode != BillingConstants.ChargeEventStatus.Void, cancellationToken);
                    if (chargeEvent != null)
                    {
                        chargeEvent.StatusCode = BillingConstants.ChargeEventStatus.Void;
                        chargeEvent.VoidedAt = now;
                        chargeEvent.VoidedBy = request.LoggedInUserName;
                        chargeEvent.VoidReason = string.IsNullOrWhiteSpace(request.Reason) ? "Order line discontinued." : request.Reason.Trim();
                        chargeEvent.UpdatedAt = now;
                        chargeEvent.UpdatedBy = request.LoggedInUserName;
                        chargeVoided = true;
                    }
                }

                await _context.SaveChangesAsync(cancellationToken);

                return new DiscontinueClinicalOrderLineResponseModel
                {
                    Success = true,
                    Message = "Order line discontinued.",
                    OrderLineId = line.OrderLineId,
                    ChargeVoided = chargeVoided,
                };
            }
            catch (Exception)
            {
                return new DiscontinueClinicalOrderLineResponseModel { Success = false, Message = "Error discontinuing order line." };
            }
        }

        // Fallback CategoryCode when the ChargeMaster line has no category of its own set.
        private static string DefaultCategoryFor(string orderType) => orderType switch
        {
            IpdConstants.ClinicalOrderType.Medication => "PHARMACY",
            IpdConstants.ClinicalOrderType.Lab => "LAB",
            IpdConstants.ClinicalOrderType.Radiology => "RADIOLOGY",
            IpdConstants.ClinicalOrderType.Procedure => "PROCEDURE",
            IpdConstants.ClinicalOrderType.Diet => "DIET",
            IpdConstants.ClinicalOrderType.Nursing => "NURSING",
            _ => orderType,
        };
    }
}
