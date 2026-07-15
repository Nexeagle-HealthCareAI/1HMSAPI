using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>
    /// Admin moderation list for one doctor's reviews — ALL reviews (hidden + visible), unlike
    /// the public endpoint. Confirms the doctor genuinely belongs to the calling hospital via
    /// DoctorDepartments before returning anything — same ownership check
    /// UpdateDoctorPublicListingHandler already uses.
    /// </summary>
    public class GetHospitalDoctorReviewsHandler : IRequestHandler<GetHospitalDoctorReviewsRequestModel, GetHospitalDoctorReviewsResponseModel>
    {
        private readonly AppDbContext _context;

        public GetHospitalDoctorReviewsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetHospitalDoctorReviewsResponseModel> Handle(GetHospitalDoctorReviewsRequestModel request, CancellationToken cancellationToken)
        {
            var belongsToHospital = await _context.DoctorDepartments
                .AnyAsync(dd => dd.DoctorID == request.DoctorId && dd.HospitalId == request.HospitalId, cancellationToken);
            if (!belongsToHospital)
                return new GetHospitalDoctorReviewsResponseModel { Success = false, Message = "Doctor not found at this hospital." };

            var reviews = await _context.DoctorReviews
                .Where(r => r.DoctorId == request.DoctorId)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new AdminReviewItem
                {
                    ReviewId = r.ReviewId,
                    AuthorName = r.AuthorName,
                    Rating = r.Rating,
                    Comment = r.Comment,
                    HelpfulCount = r.HelpfulCount,
                    IsHidden = r.IsHidden,
                    IsHospitalResponse = r.IsHospitalResponse,
                    SubmittedIp = r.SubmittedIp,
                    CreatedAt = r.CreatedAt,
                })
                .ToListAsync(cancellationToken);

            var visible = reviews.Where(r => !r.IsHidden && !r.IsHospitalResponse).ToList();

            return new GetHospitalDoctorReviewsResponseModel
            {
                Success = true,
                Reviews = reviews,
                ReviewCount = visible.Count,
                AverageRating = visible.Count > 0 ? Math.Round(visible.Average(r => r.Rating), 1) : 0,
            };
        }
    }
}
