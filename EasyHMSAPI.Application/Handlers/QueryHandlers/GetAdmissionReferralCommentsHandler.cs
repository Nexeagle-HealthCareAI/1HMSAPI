using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>All comments for one Referred Admissions board row, newest first. CreatedBy is
    /// already a plain string (same convention as AdmissionReferralStatusHistory.ChangedBy) -- no
    /// name-resolution join needed.</summary>
    public class GetAdmissionReferralCommentsHandler : IRequestHandler<GetAdmissionReferralCommentsRequestModel, GetAdmissionReferralCommentsResponseModel>
    {
        private readonly AppDbContext _context;

        public GetAdmissionReferralCommentsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetAdmissionReferralCommentsResponseModel> Handle(GetAdmissionReferralCommentsRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.ReferralId == Guid.Empty)
                    return new GetAdmissionReferralCommentsResponseModel { Success = false, Message = "HospitalId and ReferralId are required." };

                var comments = await _context.AdmissionReferralComment
                    .Where(c => c.ReferralId == request.ReferralId && c.HospitalId == request.HospitalId)
                    .OrderByDescending(c => c.CreatedAt)
                    .Select(c => new AdmissionReferralCommentItem
                    {
                        CommentId = c.CommentId,
                        CommentText = c.CommentText,
                        CreatedAt = c.CreatedAt,
                        CreatedBy = c.CreatedBy,
                    })
                    .ToListAsync(cancellationToken);

                return new GetAdmissionReferralCommentsResponseModel { Success = true, Comments = comments };
            }
            catch (Exception)
            {
                return new GetAdmissionReferralCommentsResponseModel { Success = false, Message = "Error loading comments." };
            }
        }
    }
}
