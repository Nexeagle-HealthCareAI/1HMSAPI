using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class EnhancementCommandHandlers :
        IRequestHandler<RecordEnhancementRequestRequestModel, RecordEnhancementRequestResponseModel>,
        IRequestHandler<RecordEnhancementApprovalRequestModel, RecordEnhancementApprovalResponseModel>
    {
        private readonly AppDbContext _context;

        public EnhancementCommandHandlers(AppDbContext context)
        {
            _context = context;
        }

        public async Task<RecordEnhancementRequestResponseModel> Handle(RecordEnhancementRequestRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                    return new RecordEnhancementRequestResponseModel { Success = false, Message = "HospitalId and AdmissionId are required." };
                if (request.RequestedSanctionedAmount <= 0)
                    return new RecordEnhancementRequestResponseModel { Success = false, Message = "RequestedSanctionedAmount must be greater than zero." };

                var coverage = await _context.AdmissionCoverage
                    .Where(c => c.HospitalId == request.HospitalId && c.AdmissionId == request.AdmissionId)
                    .OrderByDescending(c => c.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken);
                if (coverage == null)
                    return new RecordEnhancementRequestResponseModel { Success = false, Message = "No coverage record found for this admission." };

                var now = DateTime.UtcNow;
                coverage.EnhancementRequestedAt = now;
                coverage.EnhancementRequestedBy = request.LoggedInUserName;
                coverage.EnhancedSanctionedAmount = request.RequestedSanctionedAmount;
                coverage.EnhancementApprovedAt = null;
                coverage.EnhancementApprovedBy = null;
                coverage.UpdatedAt = now;
                coverage.UpdatedBy = request.LoggedInUserName;

                await _context.SaveChangesAsync(cancellationToken);
                return new RecordEnhancementRequestResponseModel { Success = true, Message = "Enhancement request recorded." };
            }
            catch (Exception)
            {
                return new RecordEnhancementRequestResponseModel { Success = false, Message = "Error recording the enhancement request." };
            }
        }

        public async Task<RecordEnhancementApprovalResponseModel> Handle(RecordEnhancementApprovalRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                    return new RecordEnhancementApprovalResponseModel { Success = false, Message = "HospitalId and AdmissionId are required." };

                var coverage = await _context.AdmissionCoverage
                    .Where(c => c.HospitalId == request.HospitalId && c.AdmissionId == request.AdmissionId)
                    .OrderByDescending(c => c.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken);
                if (coverage == null)
                    return new RecordEnhancementApprovalResponseModel { Success = false, Message = "No coverage record found for this admission." };
                if (coverage.EnhancementRequestedAt == null || coverage.EnhancedSanctionedAmount == null)
                    return new RecordEnhancementApprovalResponseModel { Success = false, Message = "No pending enhancement request to approve." };

                var now = DateTime.UtcNow;
                if (request.ApprovedSanctionedAmount.HasValue)
                {
                    if (request.ApprovedSanctionedAmount.Value <= 0)
                        return new RecordEnhancementApprovalResponseModel { Success = false, Message = "ApprovedSanctionedAmount must be greater than zero." };
                    coverage.EnhancedSanctionedAmount = request.ApprovedSanctionedAmount.Value;
                }
                coverage.EnhancementApprovedAt = now;
                coverage.EnhancementApprovedBy = request.LoggedInUserName;
                coverage.UpdatedAt = now;
                coverage.UpdatedBy = request.LoggedInUserName;

                await _context.SaveChangesAsync(cancellationToken);
                return new RecordEnhancementApprovalResponseModel { Success = true, Message = "Enhancement approval recorded." };
            }
            catch (Exception)
            {
                return new RecordEnhancementApprovalResponseModel { Success = false, Message = "Error recording the enhancement approval." };
            }
        }
    }
}
