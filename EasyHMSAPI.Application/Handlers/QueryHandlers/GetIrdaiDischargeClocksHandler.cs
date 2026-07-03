using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>
    /// IRDAI discharge-process clock milestones, blended from AdmissionStatusHistory (discharge
    /// decision / physical discharge — "compute off this, not snapshots") and AdmissionCoverage's
    /// 2 new timestamp pairs (claim submitted / insurer approval). Read-only report — this handler
    /// never writes; see StampIrdaiMilestoneHandler for that. Tolerant of CASH admissions (returns
    /// an explanatory empty response rather than erroring) — the frontend simply never renders
    /// this panel for CASH.
    /// </summary>
    public class GetIrdaiDischargeClocksHandler : IRequestHandler<GetIrdaiDischargeClocksRequestModel, GetIrdaiDischargeClocksResponseModel>
    {
        private readonly AppDbContext _context;

        public GetIrdaiDischargeClocksHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetIrdaiDischargeClocksResponseModel> Handle(GetIrdaiDischargeClocksRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                    return new GetIrdaiDischargeClocksResponseModel { Success = false, Message = "HospitalId and AdmissionId are required." };

                var admission = await _context.Admission
                    .FirstOrDefaultAsync(a => a.AdmissionId == request.AdmissionId && a.HospitalId == request.HospitalId, cancellationToken);
                if (admission == null)
                    return new GetIrdaiDischargeClocksResponseModel { Success = false, Message = "Admission not found." };

                if (admission.PayerType == IpdConstants.PayerType.Cash)
                {
                    return new GetIrdaiDischargeClocksResponseModel
                    {
                        Success = true,
                        PayerType = admission.PayerType,
                        Message = "IRDAI clocks apply to TPA/SCHEME admissions only.",
                    };
                }

                var history = await _context.AdmissionStatusHistory
                    .Where(h => h.HospitalId == request.HospitalId && h.AdmissionId == request.AdmissionId)
                    .OrderBy(h => h.ChangedAt)
                    .ToListAsync(cancellationToken);

                var coverage = await _context.AdmissionCoverage
                    .Where(c => c.HospitalId == request.HospitalId && c.AdmissionId == request.AdmissionId)
                    .OrderByDescending(c => c.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken);

                var dischargeDecisionAt = history
                    .Where(h => h.ToStatus == IpdConstants.AdmissionStatus.DischargeInitiated || h.ToStatus == IpdConstants.AdmissionStatus.DischargeBilled)
                    .Select(h => (DateTime?)h.ChangedAt)
                    .FirstOrDefault();

                var physicalDischargeAt = history
                    .Where(h => IpdConstants.AdmissionStatus.Terminal.Contains(h.ToStatus))
                    .Select(h => (DateTime?)h.ChangedAt)
                    .FirstOrDefault();

                var milestones = new List<(string Key, string Label, DateTime? At)>
                {
                    (IpdConstants.IrdaiClockMilestone.DischargeDecision, "Discharge decision", dischargeDecisionAt),
                    (IpdConstants.IrdaiClockMilestone.PhysicalDischarge, "Physical discharge", physicalDischargeAt ?? admission.DischargedAt),
                    (IpdConstants.IrdaiClockMilestone.ClaimSubmitted, "Claim submitted to insurer", coverage?.ClaimSubmittedAt),
                    (IpdConstants.IrdaiClockMilestone.InsurerApproval, "Insurer approval received", coverage?.InsurerApprovalAt),
                };

                var result = new List<IrdaiMilestoneModel>();
                DateTime? previous = admission.AdmittedAt;
                foreach (var (key, label, at) in milestones)
                {
                    int? durationMinutes = (at.HasValue && previous.HasValue)
                        ? (int)(at.Value - previous.Value).TotalMinutes
                        : null;
                    result.Add(new IrdaiMilestoneModel { Key = key, Label = label, At = at, DurationFromPreviousMinutes = durationMinutes });
                    if (at.HasValue) previous = at;
                }

                return new GetIrdaiDischargeClocksResponseModel { Success = true, PayerType = admission.PayerType, Milestones = result };
            }
            catch (Exception)
            {
                return new GetIrdaiDischargeClocksResponseModel { Success = false, Message = "Error loading IRDAI discharge clocks." };
            }
        }
    }
}
