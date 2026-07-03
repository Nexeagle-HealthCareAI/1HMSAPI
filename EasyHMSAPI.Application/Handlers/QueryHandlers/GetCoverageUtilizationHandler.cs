using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>
    /// Running charge total vs. the admission's sanctioned amount, for TPA/SCHEME admissions.
    /// EnhancedSanctionedAmount only becomes effective once EnhancementApprovedAt is set — a
    /// pending (requested-but-not-approved) enhancement never inflates the effective ceiling used
    /// for the approaching-limit alert. Fixed 80% threshold, no per-hospital config this phase.
    /// </summary>
    public class GetCoverageUtilizationHandler : IRequestHandler<GetCoverageUtilizationRequestModel, GetCoverageUtilizationResponseModel>
    {
        public const decimal ApproachingLimitThresholdPercent = 80m;

        private readonly AppDbContext _context;

        public GetCoverageUtilizationHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetCoverageUtilizationResponseModel> Handle(GetCoverageUtilizationRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                    return new GetCoverageUtilizationResponseModel { Success = false, Message = "HospitalId and AdmissionId are required." };

                var admission = await _context.Admission
                    .FirstOrDefaultAsync(a => a.AdmissionId == request.AdmissionId && a.HospitalId == request.HospitalId, cancellationToken);
                if (admission == null)
                    return new GetCoverageUtilizationResponseModel { Success = false, Message = "Admission not found." };

                var coverage = await _context.AdmissionCoverage
                    .FirstOrDefaultAsync(c => c.HospitalId == request.HospitalId && c.AdmissionId == request.AdmissionId, cancellationToken);
                if (coverage == null)
                {
                    return new GetCoverageUtilizationResponseModel
                    {
                        Success = true,
                        PayerType = admission.PayerType,
                        Message = "No coverage record for this admission — not applicable.",
                    };
                }

                decimal runningTotal = 0;
                if (admission.EncounterId.HasValue)
                {
                    runningTotal = await _context.BillingChargeEvent
                        .Where(c => c.HospitalId == request.HospitalId
                                 && c.EncounterId == admission.EncounterId.Value
                                 && c.StatusCode != BillingConstants.ChargeEventStatus.Void)
                        .SumAsync(c => c.NetAmount, cancellationToken);
                }

                var effectiveSanctioned = coverage.EnhancementApprovedAt.HasValue && coverage.EnhancedSanctionedAmount.HasValue
                    ? coverage.EnhancedSanctionedAmount.Value
                    : coverage.SanctionedAmount ?? 0m;

                decimal? utilizationPercent = effectiveSanctioned > 0
                    ? Math.Round(runningTotal / effectiveSanctioned * 100m, 2)
                    : null;

                return new GetCoverageUtilizationResponseModel
                {
                    Success = true,
                    PayerType = admission.PayerType,
                    SanctionedAmount = coverage.SanctionedAmount,
                    EffectiveSanctionedAmount = effectiveSanctioned,
                    RunningTotal = runningTotal,
                    UtilizationPercent = utilizationPercent,
                    IsApproachingLimit = utilizationPercent.HasValue && utilizationPercent.Value >= ApproachingLimitThresholdPercent,
                    EnhancementRequestedAt = coverage.EnhancementRequestedAt,
                    EnhancementRequestedBy = coverage.EnhancementRequestedBy,
                    EnhancedSanctionedAmount = coverage.EnhancedSanctionedAmount,
                    EnhancementApprovedAt = coverage.EnhancementApprovedAt,
                    EnhancementApprovedBy = coverage.EnhancementApprovedBy,
                };
            }
            catch (Exception)
            {
                return new GetCoverageUtilizationResponseModel { Success = false, Message = "Error computing coverage utilization." };
            }
        }
    }
}
