using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    // Handles the three doctor/desk-driven status transitions (PENDING, NOT_ADMITTED, FOLLOW_UP).
    // CONVERTED is deliberately NOT settable here — it's only ever set atomically inside
    // AdmitPatientHandler, in the same transaction that creates the real Admission, so a referral
    // can never end up CONVERTED without a linked admission to point at.
    public class UpdateAdmissionReferralStatusHandler : IRequestHandler<UpdateAdmissionReferralStatusRequestModel, UpdateAdmissionReferralStatusResponseModel>
    {
        private static readonly string[] SettableStatuses = { "PENDING", "NOT_ADMITTED", "FOLLOW_UP" };

        private readonly AppDbContext _context;

        public UpdateAdmissionReferralStatusHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<UpdateAdmissionReferralStatusResponseModel> Handle(UpdateAdmissionReferralStatusRequestModel request, CancellationToken cancellationToken)
        {
            UpdateAdmissionReferralStatusResponseModel response = new() { Success = false };
            try
            {
                var statusCode = (request.StatusCode ?? string.Empty).Trim().ToUpperInvariant();
                if (!SettableStatuses.Contains(statusCode))
                {
                    response.Message = "StatusCode must be one of PENDING, NOT_ADMITTED, FOLLOW_UP.";
                    return response;
                }

                var referral = await _context.AdmissionReferrals
                    .FirstOrDefaultAsync(r => r.ReferralId == request.ReferralId, cancellationToken);
                if (referral == null)
                {
                    response.Message = "Referral not found.";
                    return response;
                }

                if (referral.StatusCode == "CONVERTED")
                {
                    response.Message = "This referral has already been converted to an admission and can no longer be updated.";
                    return response;
                }

                if (statusCode == "NOT_ADMITTED" && string.IsNullOrWhiteSpace(request.NotAdmittedReason))
                {
                    response.Message = "NotAdmittedReason is required when marking a referral as not admitted.";
                    return response;
                }

                if (statusCode == "FOLLOW_UP" && !request.FollowUpDate.HasValue)
                {
                    response.Message = "FollowUpDate is required when scheduling a follow-up.";
                    return response;
                }

                referral.StatusCode = statusCode;
                referral.NotAdmittedReason = statusCode == "NOT_ADMITTED" ? request.NotAdmittedReason : null;
                referral.FollowUpDate = statusCode == "FOLLOW_UP" ? request.FollowUpDate : null;
                referral.FollowUpNotes = statusCode == "FOLLOW_UP" ? request.FollowUpNotes : null;
                referral.UpdatedAt = DateTime.UtcNow;
                referral.UpdatedBy = request.LoggedInUserName;

                _context.AdmissionReferralStatusHistories.Add(new AdmissionReferralStatusHistory
                {
                    HistoryId = Guid.NewGuid(),
                    ReferralId = referral.ReferralId,
                    StatusCode = statusCode,
                    ChangedAt = DateTime.UtcNow,
                    ChangedBy = request.LoggedInUserName,
                    Notes = statusCode == "NOT_ADMITTED" ? request.NotAdmittedReason : request.FollowUpNotes,
                });

                await _context.SaveChangesAsync(cancellationToken);

                response.Success = true;
                response.Message = "Referral status updated successfully.";
            }
            catch (Exception ex)
            {
                response.Message = "An error occurred: " + ex.Message + ex.InnerException + ex.StackTrace;
            }

            return response;
        }
    }
}
