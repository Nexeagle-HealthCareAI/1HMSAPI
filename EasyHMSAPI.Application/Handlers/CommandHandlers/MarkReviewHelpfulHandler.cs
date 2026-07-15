using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    // No auth beyond the public API key — matches today's UI, where any visitor can click
    // "Helpful" on any review with no gate.
    public class MarkReviewHelpfulHandler : IRequestHandler<MarkReviewHelpfulRequestModel, MarkReviewHelpfulResponseModel>
    {
        private readonly AppDbContext _context;

        public MarkReviewHelpfulHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<MarkReviewHelpfulResponseModel> Handle(MarkReviewHelpfulRequestModel request, CancellationToken cancellationToken)
        {
            var review = await _context.DoctorReviews.FirstOrDefaultAsync(r => r.ReviewId == request.ReviewId, cancellationToken);
            if (review == null)
                return new MarkReviewHelpfulResponseModel { Success = false, Message = "Review not found." };

            review.HelpfulCount++;
            await _context.SaveChangesAsync(cancellationToken);

            return new MarkReviewHelpfulResponseModel { Success = true, HelpfulCount = review.HelpfulCount };
        }
    }
}
