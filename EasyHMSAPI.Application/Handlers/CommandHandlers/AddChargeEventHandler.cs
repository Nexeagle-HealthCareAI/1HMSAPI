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

                // ── IPD ward/room/payer context (GST resolver + rate-card lookups need this) ──
                // Resolved once per request, not per line — every charge in one batch belongs to
                // the same encounter/admission.
                string? ipdWardType = null;
                decimal? ipdRoomDailyRate = null;
                string? ipdPayerType = null;
                var isIpdEncounter = string.Equals(encounter.SourceType, "Admission", StringComparison.OrdinalIgnoreCase) && encounter.SourceId.HasValue;
                if (isIpdEncounter)
                {
                    var activeBed = await (
                        from ba in _context.BedAssignment
                        join bm in _context.BedMaster on ba.BedId equals bm.BedId
                        where ba.AdmissionId == encounter.SourceId!.Value && ba.StatusCode == IpdConstants.BedAssignmentStatus.Active
                        select new { bm.WardType, ba.DailyRateSnapshot }
                    ).FirstOrDefaultAsync(cancellationToken);
                    ipdWardType = activeBed?.WardType;
                    ipdRoomDailyRate = activeBed?.DailyRateSnapshot;

                    ipdPayerType = await _context.Admission
                        .Where(a => a.AdmissionId == encounter.SourceId!.Value)
                        .Select(a => a.PayerType)
                        .FirstOrDefaultAsync(cancellationToken);
                }

                // "Tariff = f(service, payer, room class)" — payer-rate override, then a room-class
                // multiplier, both optional and IPD-only (no payer/room concept on OPD visits today).
                Dictionary<Guid, decimal> payerRateOverrideByChargeId = new();
                decimal? roomClassMultiplierPercent = null;
                if (isIpdEncounter)
                {
                    var chargeIdsForRateCard = request.Charges.Where(c => c.ChargeId.HasValue).Select(c => c.ChargeId!.Value).Distinct().ToList();
                    if (chargeIdsForRateCard.Count > 0 && !string.IsNullOrWhiteSpace(ipdPayerType))
                    {
                        payerRateOverrideByChargeId = await _context.ChargeMasterPayerRate
                            .Where(r => r.HospitalId == request.HospitalId && r.IsActive
                                && chargeIdsForRateCard.Contains(r.ChargeId) && r.PayerType == ipdPayerType)
                            .ToDictionaryAsync(r => r.ChargeId, r => r.OverrideRate, cancellationToken);
                    }

                    if (!string.IsNullOrWhiteSpace(ipdWardType))
                    {
                        roomClassMultiplierPercent = await _context.RoomClassRateMultiplier
                            .Where(r => r.HospitalId == request.HospitalId && r.RoomType == ipdWardType)
                            .Select(r => (decimal?)r.MultiplierPercent)
                            .FirstOrDefaultAsync(cancellationToken);
                    }
                }

                // ── Tax context ────────────────────────────────────────────
                var policy = await _context.BillingPolicy
                    .FirstOrDefaultAsync(p => p.HospitalId == request.HospitalId, cancellationToken);

                var policyDefaultInclusive = policy?.DefaultPriceIsTaxInclusive ?? false;
                var rounding = string.IsNullOrWhiteSpace(policy?.TaxRoundingMode) ? "ROUND" : policy!.TaxRoundingMode;
                var supplierState = policy?.PlaceOfSupplyStateCode;
                var buyerState = string.IsNullOrWhiteSpace(request.PlaceOfSupplyStateCode) ? supplierState : request.PlaceOfSupplyStateCode;
                var isInterState = GstTaxComputer.IsInterState(supplierState, buyerState);

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

                // Per-doctor consult fee is the source of truth for CONSULT lines: load it once
                // for the encounter's attending doctor so manual consult charges use the right rate.
                decimal? doctorConsultFee = null;
                if (encounter.PrimaryDoctorId.HasValue
                    && request.Charges.Any(c => string.Equals(c.CategoryCode, "CONSULT", StringComparison.OrdinalIgnoreCase)))
                {
                    doctorConsultFee = await _context.DoctorFees
                        .Where(f => f.HospitalId == request.HospitalId
                                 && f.DoctorId == encounter.PrimaryDoctorId.Value
                                 && f.FeeType == "OPD_CONSULT"
                                 && f.IsActive)
                        .Select(f => (decimal?)f.Amount)
                        .FirstOrDefaultAsync(cancellationToken);
                }

                foreach (var charge in request.Charges)
                {
                    var isConsult = string.Equals(charge.CategoryCode, "CONSULT", StringComparison.OrdinalIgnoreCase);
                    var isDoctorConsultFee = isConsult && doctorConsultFee.HasValue && doctorConsultFee.Value > 0;
                    var rate = isDoctorConsultFee ? doctorConsultFee!.Value : charge.Rate;

                    // "Tariff = f(service, payer, room class)" — payer-rate override then the
                    // room-class multiplier, on top of whatever rate the caller resolved. Skipped
                    // for the doctor-consult-fee case above, which is already a specific, final rate.
                    if (!isDoctorConsultFee)
                    {
                        if (charge.ChargeId.HasValue && payerRateOverrideByChargeId.TryGetValue(charge.ChargeId.Value, out var payerRate))
                            rate = payerRate;
                        if (roomClassMultiplierPercent.HasValue)
                            rate = Math.Round(rate * roomClassMultiplierPercent.Value / 100m, 2);
                    }

                    var gross = charge.Qty * rate;
                    var discount = Math.Round(gross * (charge.DiscountPercent / 100m), 2);
                    var net = gross - discount;

                    ChargeMaster? master = (charge.ChargeId.HasValue && masters.TryGetValue(charge.ChargeId.Value, out var m)) ? m : null;
                    var hsn = !string.IsNullOrWhiteSpace(charge.HsnSacCode) ? charge.HsnSacCode!.Trim() : master?.HsnSacCode;
                    var itemGstRate = charge.GstRate ?? (master?.IsTaxable == true ? master.GstSlabPercent : null);
                    var itemIsTaxable = itemGstRate.HasValue && itemGstRate.Value > 0m;

                    var isPharmacyItem = string.Equals(master?.AppliesTo, "PHARMACY", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(charge.CategoryCode, "PHARMACY", StringComparison.OrdinalIgnoreCase);
                    var effectiveSourceModule = isPharmacyItem
                        ? (isIpdEncounter ? BillingConstants.SourceModule.PharmacyIpd : BillingConstants.SourceModule.PharmacyCounter)
                        : sourceModule;

                    // Resolves ICU/room-threshold/pharmacy exemptions on top of the item's own
                    // configured GST treatment — see GstResolver's remarks for full rule precedence.
                    // isBundled is always false this phase (package billing deferred).
                    var gstTreatment = GstResolver.Resolve(charge.CategoryCode, ipdWardType, ipdRoomDailyRate,
                        effectiveSourceModule, isBundled: false, itemGstRate, itemIsTaxable);
                    var gstRate = gstTreatment.IsExempt ? null : (gstTreatment.EffectiveGstRatePercent ?? itemGstRate);

                    var taxInclusive = charge.TaxInclusive ?? master?.TaxInclusive ?? policyDefaultInclusive;
                    var taxable = gstRate.HasValue && gstRate.Value > 0m
                        ? GstTaxComputer.Compute(net, gstRate, taxInclusive, isInterState, rounding)
                        : new GstTaxComputer.GstLineResult(net, 0, 0, 0, 0, net);

                    // Incentive: per-line override → ChargeMaster default → none.
                    var incentive = charge.IncentiveAmount ?? master?.IncentiveAmount;

                    // Discount cap: per-charge ChargeMaster cap, else no cap (100%).
                    var effectiveCap = master?.MaxDiscountPercent ?? 100m;
                    var needsApproval = charge.DiscountPercent > effectiveCap;

                    var chargeEvent = new BillingChargeEvent
                    {
                        ChargeEventId = Guid.NewGuid(),
                        HospitalId = request.HospitalId,
                        PatientId = request.PatientId,
                        EncounterId = request.EncounterId,
                        ChargeId = charge.ChargeId,
                        SourceModule = effectiveSourceModule,
                        CategoryCode = charge.CategoryCode,
                        DisplayName = charge.DisplayName,
                        Qty = charge.Qty,
                        UnitPrice = rate,
                        GrossAmount = gross,
                        DiscountAmount = discount,
                        NetAmount = net,
                        IncentiveAmount = incentive,
                        AttributedDoctorId = charge.AttributedDoctorId,
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

                    // Consultant incentive accrual: insert-only, same pattern as every other
                    // accrual ledger this session. Best-effort — only when a doctor is attributed.
                    if (charge.AttributedDoctorId.HasValue && incentive.HasValue && incentive.Value > 0)
                    {
                        _context.ConsultantIncentiveLedger.Add(new ConsultantIncentiveLedger
                        {
                            ConsultantIncentiveLedgerId = Guid.NewGuid(),
                            HospitalId = request.HospitalId,
                            DoctorId = charge.AttributedDoctorId.Value,
                            PatientId = request.PatientId!,
                            EncounterId = request.EncounterId,
                            ChargeEventId = chargeEvent.ChargeEventId,
                            IncentiveAmount = incentive.Value,
                            StatusCode = "ACCRUED",
                            AccruedAt = now,
                            CreatedAt = now,
                            CreatedBy = request.LoggedInUserName,
                            UpdatedAt = now,
                            UpdatedBy = request.LoggedInUserName,
                        });
                    }

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
                        UnitPrice = rate,
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
