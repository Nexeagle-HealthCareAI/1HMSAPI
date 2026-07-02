using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>Stamps a claim-submitted / insurer-approval milestone timestamp on the
    /// admission's AdmissionCoverage row. Small, separate from the query handler (CQRS split
    /// already used throughout this codebase).</summary>
    public class StampIrdaiMilestoneHandler : IRequestHandler<StampIrdaiMilestoneRequestModel, StampIrdaiMilestoneResponseModel>
    {
        private readonly AppDbContext _context;

        public StampIrdaiMilestoneHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<StampIrdaiMilestoneResponseModel> Handle(StampIrdaiMilestoneRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                    return new StampIrdaiMilestoneResponseModel { Success = false, Message = "HospitalId and AdmissionId are required." };

                var milestoneKey = request.MilestoneKey?.Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(milestoneKey) || !IpdConstants.IrdaiClockMilestone.Stampable.Contains(milestoneKey))
                    return new StampIrdaiMilestoneResponseModel { Success = false, Message = "Invalid milestone key." };

                var admission = await _context.Admission
                    .FirstOrDefaultAsync(a => a.AdmissionId == request.AdmissionId && a.HospitalId == request.HospitalId, cancellationToken);
                if (admission == null)
                    return new StampIrdaiMilestoneResponseModel { Success = false, Message = "Admission not found." };
                if (admission.PayerType == IpdConstants.PayerType.Cash)
                    return new StampIrdaiMilestoneResponseModel { Success = false, Message = "IRDAI clocks apply to TPA/SCHEME admissions only." };

                var coverage = await _context.AdmissionCoverage
                    .Where(c => c.HospitalId == request.HospitalId && c.AdmissionId == request.AdmissionId)
                    .OrderByDescending(c => c.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken);
                if (coverage == null)
                    return new StampIrdaiMilestoneResponseModel { Success = false, Message = "No coverage record found for this admission." };

                var now = DateTime.UtcNow;
                var at = request.At ?? now;

                if (milestoneKey == IpdConstants.IrdaiClockMilestone.ClaimSubmitted)
                {
                    coverage.ClaimSubmittedAt = at;
                    coverage.ClaimSubmittedBy = request.LoggedInUserName;
                }
                else
                {
                    coverage.InsurerApprovalAt = at;
                    coverage.InsurerApprovalBy = request.LoggedInUserName;
                }
                coverage.UpdatedAt = now;
                coverage.UpdatedBy = request.LoggedInUserName;

                await _context.SaveChangesAsync(cancellationToken);

                return new StampIrdaiMilestoneResponseModel { Success = true, Message = "Milestone recorded." };
            }
            catch (Exception)
            {
                return new StampIrdaiMilestoneResponseModel { Success = false, Message = "Error recording the milestone." };
            }
        }
    }
}
