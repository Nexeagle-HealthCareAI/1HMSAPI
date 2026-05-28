using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Data.Services;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class AddChargeEventHandler : IRequestHandler<AddChargeEventRequestModel, AddChargeEventResponseModel>
    {
        private readonly AppDbContext _context;

        public AddChargeEventHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<AddChargeEventResponseModel> Handle(AddChargeEventRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.Charges == null || request.Charges.Count == 0)
                    return new AddChargeEventResponseModel { Success = false, Message = "No charges provided." };

                var encounter = await _context.Encounter
                    .FirstOrDefaultAsync(e => e.EncounterId == request.EncounterId
                                           && e.HospitalId == request.HospitalId
                                           && e.PatientId == request.PatientId, cancellationToken);
                if (encounter == null)
                    return new AddChargeEventResponseModel { Success = false, Message = "Encounter not found for the given hospital/patient." };

                if (encounter.StatusCode != BillingConstants.EncounterStatus.Open)
                    return new AddChargeEventResponseModel { Success = false, Message = $"Encounter is not open (current status: {encounter.StatusCode})." };

                var sourceModule = string.IsNullOrWhiteSpace(encounter.EncounterTypeCode)
                    ? BillingConstants.SourceModule.Manual
                    : encounter.EncounterTypeCode!;

                // ── Tax context ────────────────────────────────────────────
                var policy = await _context.BillingPolicy
                    .FirstOrDefaultAsync(p => p.HospitalId == request.HospitalId, cancellationToken);

                var policyDefaultInclusive = policy?.DefaultPriceIsTaxInclusive ?? false;
                var rounding = string.IsNullOrWhiteSpace(policy?.TaxRoundingMode) ? "ROUND" : policy!.TaxRoundingMode;
                var supplierState = policy?.PlaceOfSupplyStateCode;
                var buyerState = string.IsNullOrWhiteSpace(request.PlaceOfSupplyStateCode) ? supplierState : request.PlaceOfSupplyStateCode;
                var isInterState = GstTaxComputer.IsInterState(supplierState, buyerState);
                var policyMaxDiscount = policy?.MaxAutoDiscountPercent ?? 100m;

                // Pre-load referenced ChargeMaster rows (rate/HSN/GST/incentive snapshot source).
                var chargeIds = request.Charges.Where(c => c.ChargeId.HasValue).Select(c => c.ChargeId!.Value).Distinct().ToList();
                var masters = chargeIds.Count == 0
                    ? new Dictionary<Guid, ChargeMaster>()
                    : await _context.ChargeMaster
                        .Where(m => m.HospitalId == request.HospitalId && chargeIds.Contains(m.ChargeId))
                        .ToDictionaryAsync(m => m.ChargeId, cancellationToken);

                var now = DateTime.UtcNow;
                var details = new List<ChargeEventDetail>();
                decimal totalGross = 0, totalDiscount = 0, totalNet = 0, totalIncentive = 0;
                decimal totalTaxable = 0, totalCgst = 0, totalSgst = 0, totalIgst = 0, totalTax = 0;

                foreach (var charge in request.Charges)
                {
                    var gross = charge.Qty * charge.Rate;
                    var discount = Math.Round(gross * (charge.DiscountPercent / 100m), 2);
                    var net = gross - discount;

                    ChargeMaster? master = (charge.ChargeId.HasValue && masters.TryGetValue(charge.ChargeId.Value, out var m)) ? m : null;
                    var hsn = !string.IsNullOrWhiteSpace(charge.HsnSacCode) ? charge.HsnSacCode!.Trim() : master?.HsnSacCode;
                    var gstRate = charge.GstRate ?? (master?.IsTaxable == true ? master.GstSlabPercent : null);
                    var taxInclusive = charge.TaxInclusive ?? master?.TaxInclusive ?? policyDefaultInclusive;
                    var taxable = gstRate.HasValue && gstRate.Value > 0m
                        ? GstTaxComputer.Compute(net, gstRate, taxInclusive, isInterState, rounding)
                        : new GstTaxComputer.GstLineResult(net, 0, 0, 0, 0, net);

                    // Incentive: per-line override → ChargeMaster default → none.
                    var incentive = charge.IncentiveAmount ?? master?.IncentiveAmount;

                    var effectiveCap = master?.MaxDiscountPercent ?? policyMaxDiscount;
                    var needsApproval = charge.DiscountPercent > effectiveCap;

                    var chargeEvent = new BillingChargeEvent
                    {
                        ChargeEventId = Guid.NewGuid(),
                        HospitalId = request.HospitalId,
                        PatientId = request.PatientId,
                        EncounterId = request.EncounterId,
                        SourceModule = sourceModule,
                        CategoryCode = charge.CategoryCode,
                        DisplayName = charge.DisplayName,
                        Qty = charge.Qty,
                        UnitPrice = charge.Rate,
                        DiscountAmount = discount,
                        NetAmount = net,
                        IncentiveAmount = incentive,
                        HsnSacCode = hsn,
                        GstRate = gstRate,
                        TaxableAmount = taxable.TaxableAmount,
                        CgstAmount = taxable.CgstAmount,
                        SgstAmount = taxable.SgstAmount,
                        IgstAmount = taxable.IgstAmount,
                        TaxAmount = taxable.TaxAmount,
                        IsTaxInclusive = taxInclusive,
                        IsInterState = isInterState,
                        StatusCode = BillingConstants.ChargeEventStatus.Posted,
                        ServiceDate = now,
                        PostedAt = now,
                        PostedBy = request.LoggedInUserName,
                        CreatedAt = now,
                        CreatedBy = request.LoggedInUserName,
                        UpdatedAt = now,
                        UpdatedBy = request.LoggedInUserName
                    };
                    _context.BillingChargeEvent.Add(chargeEvent);

                    Guid? approvalId = null;
                    if (needsApproval)
                    {
                        var approval = new DiscountApproval
                        {
                            DiscountApprovalId = Guid.NewGuid(),
                            HospitalId = request.HospitalId,
                            ChargeEventId = chargeEvent.ChargeEventId,
                            PatientId = request.PatientId,
                            EncounterId = request.EncounterId,
                            GrossAmount = gross,
                            RequestedDiscountPercent = charge.DiscountPercent,
                            RequestedDiscountAmount = discount,
                            CapPercent = effectiveCap,
                            OverByPercent = charge.DiscountPercent - effectiveCap,
                            Reason = string.IsNullOrWhiteSpace(charge.DiscountReason) ? null : charge.DiscountReason.Trim(),
                            RequestedBy = request.LoggedInUserName,
                            RequestedByUserId = request.LoggedInUserId,
                            RequestedAt = now,
                            Status = "PENDING",
                            CreatedAt = now,
                            UpdatedAt = now,
                        };
                        _context.DiscountApproval.Add(approval);
                        approvalId = approval.DiscountApprovalId;
                    }

                    totalGross += gross;
                    totalDiscount += discount;
                    totalNet += net;
                    totalIncentive += incentive ?? 0m;
                    totalTaxable += taxable.TaxableAmount;
                    totalCgst += taxable.CgstAmount;
                    totalSgst += taxable.SgstAmount;
                    totalIgst += taxable.IgstAmount;
                    totalTax += taxable.TaxAmount;

                    details.Add(new ChargeEventDetail
                    {
                        ChargeEventId = chargeEvent.ChargeEventId,
                        DisplayName = charge.DisplayName,
                        Qty = charge.Qty,
                        UnitPrice = charge.Rate,
                        GrossAmount = gross,
                        DiscountAmount = discount,
                        NetAmount = net,
                        IncentiveAmount = incentive,
                        HsnSacCode = hsn,
                        GstRate = gstRate,
                        TaxableAmount = taxable.TaxableAmount,
                        CgstAmount = taxable.CgstAmount,
                        SgstAmount = taxable.SgstAmount,
                        IgstAmount = taxable.IgstAmount,
                        TaxAmount = taxable.TaxAmount,
                        IsTaxInclusive = taxInclusive,
                        IsInterState = isInterState,
                        DiscountApprovalId = approvalId,
                        DiscountApprovalRequired = needsApproval,
                        DiscountCapPercent = needsApproval ? effectiveCap : (decimal?)null,
                    });
                }

                await _context.SaveChangesAsync(cancellationToken);

                return new AddChargeEventResponseModel
                {
                    Success = true,
                    Message = "Charges posted successfully.",
                    Data = new AddChargesData
                    {
                        EncounterId = request.EncounterId,
                        ChargeCount = details.Count,
                        TotalGross = totalGross,
                        TotalDiscount = totalDiscount,
                        TotalNet = totalNet,
                        TotalIncentive = totalIncentive,
                        TotalTaxable = totalTaxable,
                        TotalCgst = totalCgst,
                        TotalSgst = totalSgst,
                        TotalIgst = totalIgst,
                        TotalTax = totalTax,
                        ChargeEvents = details
                    }
                };
            }
            catch (Exception)
            {
                return new AddChargeEventResponseModel { Success = false, Message = "Error posting charges." };
            }
        }
    }
}
