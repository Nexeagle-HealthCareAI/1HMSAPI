using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
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
                        if (admission.EncounterId.HasValue)
                        {
                            var chargeableIndices = Enumerable.Range(0, lines.Count).Where(i => lines[i].ChargeId.HasValue).ToList();
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
                            }
                        }

                        await _context.SaveChangesAsync(cancellationToken);
                        await tx.CommitAsync(cancellationToken);

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
