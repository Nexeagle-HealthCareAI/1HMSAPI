using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>
    /// Adds a comment against a Referred Admissions board row. Insert-only, no transaction needed --
    /// a single row insert, no concurrent-uniqueness concern like the doctor/bed assignment span-row
    /// tables.
    /// </summary>
    public class AdmissionReferralCommentCommandHandlers : IRequestHandler<AddAdmissionReferralCommentRequestModel, AddAdmissionReferralCommentResponseModel>
    {
        private readonly AppDbContext _context;

        public AdmissionReferralCommentCommandHandlers(AppDbContext context)
        {
            _context = context;
        }

        public async Task<AddAdmissionReferralCommentResponseModel> Handle(AddAdmissionReferralCommentRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.ReferralId == Guid.Empty)
                    return new AddAdmissionReferralCommentResponseModel { Success = false, Message = "HospitalId and ReferralId are required." };
                if (string.IsNullOrWhiteSpace(request.CommentText))
                    return new AddAdmissionReferralCommentResponseModel { Success = false, Message = "Comment text is required." };

                var referralExists = await _context.AdmissionReferrals
                    .AnyAsync(r => r.ReferralId == request.ReferralId && r.HospitalId == request.HospitalId, cancellationToken);
                if (!referralExists)
                    return new AddAdmissionReferralCommentResponseModel { Success = false, Message = "Referral not found." };

                var now = DateTime.UtcNow;
                var comment = new AdmissionReferralComment
                {
                    CommentId = Guid.NewGuid(),
                    ReferralId = request.ReferralId,
                    HospitalId = request.HospitalId,
                    CommentText = request.CommentText.Trim(),
                    CreatedAt = now,
                    CreatedBy = request.LoggedInUserName,
                };
                _context.AdmissionReferralComment.Add(comment);
                await _context.SaveChangesAsync(cancellationToken);

                return new AddAdmissionReferralCommentResponseModel
                {
                    Success = true,
                    Message = "Comment added.",
                    CommentId = comment.CommentId,
                    CreatedAt = comment.CreatedAt,
                };
            }
            catch (Exception)
            {
                return new AddAdmissionReferralCommentResponseModel { Success = false, Message = "Error adding comment." };
            }
        }
    }
}
