using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>
    /// Public review list for one doctor — non-hidden reviews only, newest first, plus the
    /// computed average/count (which GetPublicDoctorsHandler and GetPublicDirectoryDoctorsHandler
    /// also surface per-doctor via the same non-hidden filter).
    /// </summary>
    public class GetPublicDoctorReviewsHandler : IRequestHandler<GetPublicDoctorReviewsRequestModel, GetPublicDoctorReviewsResponseModel>
    {
        private readonly AppDbContext _context;

        public GetPublicDoctorReviewsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetPublicDoctorReviewsResponseModel> Handle(GetPublicDoctorReviewsRequestModel request, CancellationToken cancellationToken)
        {
            var reviews = await _context.DoctorReviews
                .Where(r => r.DoctorId == request.DoctorId && !r.IsHidden)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new PublicReviewItem
                {
                    ReviewId = r.ReviewId,
                    AuthorName = r.AuthorName,
                    Rating = r.Rating,
                    Comment = r.Comment,
                    HelpfulCount = r.HelpfulCount,
                    IsHospitalResponse = r.IsHospitalResponse,
                    CreatedAt = r.CreatedAt,
                })
                .ToListAsync(cancellationToken);

            // A lone/unpaired UTF-16 surrogate in an older AuthorName/Comment (stores fine in SQL
            // Server, throws on JSON serialization) would otherwise 500 every read of this doctor's
            // whole review list over one bad character — sanitize defensively regardless of how it
            // got there. See TextSanitizer.
            foreach (var r in reviews)
            {
                r.AuthorName = TextSanitizer.StripInvalidSurrogates(r.AuthorName);
                r.Comment = TextSanitizer.StripInvalidSurrogates(r.Comment);
            }

            var ratedReviews = reviews.Where(r => !r.IsHospitalResponse).ToList();

            return new GetPublicDoctorReviewsResponseModel
            {
                Success = true,
                Reviews = reviews,
                ReviewCount = ratedReviews.Count,
                AverageRating = ratedReviews.Count > 0 ? Math.Round(ratedReviews.Average(r => r.Rating), 1) : 0,
            };
        }
    }
}
