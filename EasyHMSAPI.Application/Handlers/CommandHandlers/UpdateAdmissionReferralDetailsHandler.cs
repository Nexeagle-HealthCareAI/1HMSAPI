using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    // Lets a doctor edit an existing PENDING referral's details (procedure, OT plan, probable
    // date, case type, notes) in place, so reopening AdviseAdmissionSheet for a patient who was
    // already advised updates the same row instead of AdviseAdmissionHandler creating a duplicate.
    public class UpdateAdmissionReferralDetailsHandler : IRequestHandler<UpdateAdmissionReferralDetailsRequestModel, UpdateAdmissionReferralDetailsResponseModel>
    {
        private static readonly string[] ValidCaseTypes = { "EMERGENCY", "PLANNED", "URGENT" };

        private readonly AppDbContext _context;

        public UpdateAdmissionReferralDetailsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<UpdateAdmissionReferralDetailsResponseModel> Handle(UpdateAdmissionReferralDetailsRequestModel request, CancellationToken cancellationToken)
        {
            UpdateAdmissionReferralDetailsResponseModel response = new() { Success = false };
            try
            {
                var caseType = (request.CaseType ?? string.Empty).Trim().ToUpperInvariant();
                if (!ValidCaseTypes.Contains(caseType))
                {
                    response.Message = "CaseType must be one of EMERGENCY, PLANNED, URGENT.";
                    return response;
                }

                var referral = await _context.AdmissionReferrals
                    .FirstOrDefaultAsync(r => r.ReferralId == request.ReferralId && r.HospitalId == request.HospitalId, cancellationToken);
                if (referral == null)
                {
                    response.Message = "Referral not found.";
                    return response;
                }

                if (referral.StatusCode == "CONVERTED")
                {
                    response.Message = "This referral has already been converted to an admission and can no longer be edited.";
                    return response;
                }

                var procedureName = request.ProcedureName;
                if (request.OtPlanId.HasValue && request.OtPlanId != Guid.Empty)
                {
                    var plan = await _context.OTPlans
                        .FirstOrDefaultAsync(p => p.OtPlanId == request.OtPlanId && p.HospitalId == request.HospitalId, cancellationToken);
                    if (plan == null)
                    {
                        response.Message = "Selected OT Plan not found.";
                        return response;
                    }
                    if (string.IsNullOrWhiteSpace(procedureName))
                        procedureName = plan.ProcedureName;
                }

                referral.OtPlanId = request.OtPlanId;
                referral.PackageTypeId = request.PackageTypeId;
                referral.ProcedureName = procedureName;
                referral.ProbableAdmissionDate = request.ProbableAdmissionDate;
                referral.CaseType = caseType;
                referral.Notes = request.Notes;
                referral.UpdatedAt = DateTime.UtcNow;
                referral.UpdatedBy = request.LoggedInUserName;

                await _context.SaveChangesAsync(cancellationToken);

                response.Success = true;
                response.Message = "Admission advice updated successfully.";
            }
            catch (Exception ex)
            {
                response.Message = "An error occurred: " + ex.Message + ex.InnerException + ex.StackTrace;
            }

            return response;
        }
    }
}
