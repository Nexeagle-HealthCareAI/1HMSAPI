using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>
    /// Admin, authenticated write — lets a hospital admin post an official comment against one of
    /// their own doctors from Public Directory's review panel. Always tagged IsHospitalResponse so
    /// it's excluded from the average-rating/review-count aggregates (it's not patient sentiment)
    /// and always rendered client-side as "Hospital Response", never attributed to a person.
    /// Rating is stored as a fixed placeholder (the column is NOT NULL with a 1-5 CHECK) but is
    /// never read back for hospital-response rows since every aggregate excludes them.
    /// </summary>
    public class SubmitHospitalResponseHandler : IRequestHandler<SubmitHospitalResponseRequestModel, SubmitHospitalResponseResponseModel>
    {
        private readonly AppDbContext _context;

        public SubmitHospitalResponseHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<SubmitHospitalResponseResponseModel> Handle(SubmitHospitalResponseRequestModel request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Comment))
                return new SubmitHospitalResponseResponseModel { Success = false, Message = "Comment is required." };

            var belongsToHospital = await _context.DoctorDepartments
                .AnyAsync(dd => dd.DoctorID == request.DoctorId && dd.HospitalId == request.HospitalId, cancellationToken);
            if (!belongsToHospital)
                return new SubmitHospitalResponseResponseModel { Success = false, Message = "Doctor not found at this hospital." };

            var review = new DoctorReview
            {
                ReviewId = Guid.NewGuid(),
                HospitalId = request.HospitalId,
                DoctorId = request.DoctorId,
                AuthorName = null,
                Rating = 5,
                Comment = request.Comment.Trim(),
                HelpfulCount = 0,
                IsHidden = false,
                IsHospitalResponse = true,
                SubmittedIp = null,
                CreatedAt = DateTime.UtcNow,
            };
            _context.DoctorReviews.Add(review);
            await _context.SaveChangesAsync(cancellationToken);

            return new SubmitHospitalResponseResponseModel { Success = true, Message = "Response posted.", ReviewId = review.ReviewId };
        }
    }
}
