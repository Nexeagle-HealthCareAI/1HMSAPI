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
    /// </summary>
    public class GetDischargeTpaSplitHandler : IRequestHandler<GetDischargeTpaSplitRequestModel, GetDischargeTpaSplitResponseModel>
    {
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

                var lines = new List<TpaSplitLineModel>();
                decimal payableTotal = 0, nonPayableTotal = 0, unclassifiedTotal = 0;

                foreach (var e in events)
                {
                    var net = e.NetAmount;
                    bool? isPayable = e.ChargeId.HasValue && payableByChargeId.TryGetValue(e.ChargeId.Value, out var p) ? p : (bool?)null;

                    if (isPayable == true) payableTotal += net;
                    else if (isPayable == false) nonPayableTotal += net;
                    else unclassifiedTotal += net;

                    lines.Add(new TpaSplitLineModel
                    {
                        DisplayName = e.DisplayName,
                        CategoryCode = e.CategoryCode,
                        NetAmount = net,
                        IsIRDAIPayable = isPayable,
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
