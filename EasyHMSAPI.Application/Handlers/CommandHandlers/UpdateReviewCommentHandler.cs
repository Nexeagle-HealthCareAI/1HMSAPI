using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>
    /// Public, anonymous — lets the submitter of a rating-only review attach a comment
    /// afterward. No login, same trust model as SubmitDoctorReviewHandler's "anyone can
    /// submit": possession of the ReviewId (only ever handed to the browser that just
    /// submitted it, via SubmitDoctorReviewResponseModel) is the only "ownership" check.
    /// Never touches Rating/AuthorName/IsHidden, and never targets a hospital-response row
    /// (those aren't reachable by a public caller's own ReviewId in practice, but guarded
    /// anyway since this is an anonymous, unauthenticated write).
    /// </summary>
    public class UpdateReviewCommentHandler : IRequestHandler<UpdateReviewCommentRequestModel, UpdateReviewCommentResponseModel>
    {
        private readonly AppDbContext _context;

        public UpdateReviewCommentHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<UpdateReviewCommentResponseModel> Handle(UpdateReviewCommentRequestModel request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Comment))
                return new UpdateReviewCommentResponseModel { Success = false, Message = "Comment is required." };

            var review = await _context.DoctorReviews
                .FirstOrDefaultAsync(r => r.ReviewId == request.ReviewId && r.DoctorId == request.DoctorId && !r.IsHospitalResponse, cancellationToken);
            if (review == null)
                return new UpdateReviewCommentResponseModel { Success = false, Message = "Review not found." };

            review.Comment = request.Comment.Trim();
            await _context.SaveChangesAsync(cancellationToken);

            return new UpdateReviewCommentResponseModel { Success = true, Message = "Comment saved." };
        }
    }
}
