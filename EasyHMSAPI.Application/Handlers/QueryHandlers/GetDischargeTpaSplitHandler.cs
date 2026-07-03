using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>
    /// TPA payable/non-payable split for an admission's discharge — a flat, hospital-wide
    /// IRDAI-list-style classification (ChargeMaster.IsIRDAIPayable), not a per-patient-plan
    /// proration. Charges with no ChargeId link (manual free-text charges, or legacy rows
    /// predating this column) are surfaced as Unclassified, never silently bucketed either way.
    ///
    /// Room-rent proportionate deduction: when the admission has an EntitledRoomCategory and a
    /// BED-category charge's actual ward is a non-ICU category ranked above it (patient upgraded
    /// beyond entitlement), only the entitled-tier's representative daily rate stays Payable — the
    /// differential becomes NonPayable. ICU-family wards are never subject to this (clinical
    /// necessity, not a room upgrade). Falls back to plain IsIRDAIPayable classification whenever
    /// entitlement isn't captured or no representative entitled-tier rate can be found — never
    /// guesses at a proration it can't actually compute.
    /// </summary>
    public class GetDischargeTpaSplitHandler : IRequestHandler<GetDischargeTpaSplitRequestModel, GetDischargeTpaSplitResponseModel>
    {
        // Ordinal rank for non-ICU room categories — higher rank = costlier/more private.
        private static readonly Dictionary<string, int> RoomRank = new()
        {
            [IpdConstants.WardType.General] = 1,
            [IpdConstants.WardType.SemiPrivate] = 2,
            [IpdConstants.WardType.Private] = 3,
        };

        private readonly AppDbContext _context;

        public GetDischargeTpaSplitHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetDischargeTpaSplitResponseModel> Handle(GetDischargeTpaSplitRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                    return new GetDischargeTpaSplitResponseModel { Success = false, Message = "HospitalId and AdmissionId are required." };

                var admission = await _context.Admission
                    .FirstOrDefaultAsync(a => a.AdmissionId == request.AdmissionId && a.HospitalId == request.HospitalId, cancellationToken);
                if (admission == null)
                    return new GetDischargeTpaSplitResponseModel { Success = false, Message = "Admission not found." };

                if (admission.EncounterId == null)
                {
                    return new GetDischargeTpaSplitResponseModel
                    {
                        Success = true,
                        PayerType = admission.PayerType,
                        Message = "This admission has no encounter — no charges to split.",
                    };
                }

                var events = await _context.BillingChargeEvent
                    .Where(c => c.HospitalId == request.HospitalId && c.EncounterId == admission.EncounterId.Value
                        && c.StatusCode != BillingConstants.ChargeEventStatus.Void)
                    .ToListAsync(cancellationToken);

                var chargeIds = events.Where(e => e.ChargeId.HasValue).Select(e => e.ChargeId!.Value).Distinct().ToList();
                var payableByChargeId = chargeIds.Count == 0
                    ? new Dictionary<Guid, bool>()
                    : await _context.ChargeMaster
                        .Where(m => m.HospitalId == request.HospitalId && chargeIds.Contains(m.ChargeId))
                        .ToDictionaryAsync(m => m.ChargeId, m => m.IsIRDAIPayable, cancellationToken);

                // ── Proportionate room-rent deduction context ───────────────────────────────
                var coverage = await _context.AdmissionCoverage
                    .FirstOrDefaultAsync(c => c.HospitalId == request.HospitalId && c.AdmissionId == request.AdmissionId, cancellationToken);
                var entitledCategory = coverage?.EntitledRoomCategory?.Trim().ToUpperInvariant();
                var entitledRank = entitledCategory != null && RoomRank.TryGetValue(entitledCategory, out var er) ? er : (int?)null;

                // Bed charge events carry the BedAssignment.AssignmentId as SourceRefId — resolve each
                // one's actual ward/room type + a representative rate for the entitled category, if any.
                Dictionary<Guid, string?> wardTypeByAssignmentId = new();
                decimal? entitledTierDailyRate = null;
                if (entitledRank.HasValue)
                {
                    var bedEventAssignmentIds = events
                        .Where(e => string.Equals(e.CategoryCode, "BED", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(e.SourceRefId))
                        .Select(e => e.SourceRefId!)
                        .Select(id => Guid.TryParse(id, out var g) ? g : (Guid?)null)
                        .Where(g => g.HasValue)
                        .Select(g => g!.Value)
                        .Distinct()
                        .ToList();

                    if (bedEventAssignmentIds.Count > 0)
                    {
                        wardTypeByAssignmentId = await _context.BedAssignment
                            .Where(ba => bedEventAssignmentIds.Contains(ba.AssignmentId))
                            .Join(_context.BedMaster, ba => ba.BedId, bm => bm.BedId, (ba, bm) => new { ba.AssignmentId, bm.WardType })
                            .ToDictionaryAsync(x => x.AssignmentId, x => x.WardType, cancellationToken);
                    }

                    entitledTierDailyRate = await _context.BedMaster
                        .Where(bm => bm.HospitalId == request.HospitalId && bm.WardType == entitledCategory && bm.IsActive)
                        .OrderBy(bm => bm.WardRoomDailyRate)
                        .Select(bm => (decimal?)bm.WardRoomDailyRate)
                        .FirstOrDefaultAsync(cancellationToken);
                }

                var lines = new List<TpaSplitLineModel>();
                decimal payableTotal = 0, nonPayableTotal = 0, unclassifiedTotal = 0;

                foreach (var e in events)
                {
                    var net = e.NetAmount;
                    bool? isPayable = e.ChargeId.HasValue && payableByChargeId.TryGetValue(e.ChargeId.Value, out var p) ? p : (bool?)null;

                    // Attempt the proportionate split only for BED charges above the entitled tier —
                    // never for ICU-family wards (clinical necessity, not a chosen upgrade).
                    decimal? proratedNonPayable = null;
                    if (entitledRank.HasValue && entitledTierDailyRate.HasValue
                        && string.Equals(e.CategoryCode, "BED", StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(e.SourceRefId)
                        && Guid.TryParse(e.SourceRefId, out var assignmentId)
                        && wardTypeByAssignmentId.TryGetValue(assignmentId, out var actualWard)
                        && !IpdConstants.WardType.IsIcuFamily(actualWard)
                        && actualWard != null && RoomRank.TryGetValue(actualWard.Trim().ToUpperInvariant(), out var actualRank)
                        && actualRank > entitledRank.Value)
                    {
                        var payablePortion = Math.Min(net, entitledTierDailyRate.Value);
                        proratedNonPayable = Math.Max(0, net - payablePortion);
                        payableTotal += payablePortion;
                        nonPayableTotal += proratedNonPayable.Value;
                    }
                    else if (isPayable == true) payableTotal += net;
                    else if (isPayable == false) nonPayableTotal += net;
                    else unclassifiedTotal += net;

                    lines.Add(new TpaSplitLineModel
                    {
                        DisplayName = e.DisplayName,
                        CategoryCode = e.CategoryCode,
                        NetAmount = net,
                        IsIRDAIPayable = proratedNonPayable.HasValue ? false : isPayable,
                        ProratedNonPayableAmount = proratedNonPayable,
                    });
                }

                return new GetDischargeTpaSplitResponseModel
                {
                    Success = true,
                    PayerType = admission.PayerType,
                    PayableTotal = payableTotal,
                    NonPayableTotal = nonPayableTotal,
                    UnclassifiedTotal = unclassifiedTotal,
                    Lines = lines,
                };
            }
            catch (Exception)
            {
                return new GetDischargeTpaSplitResponseModel { Success = false, Message = "Error computing the TPA split." };
            }
        }
    }
}
