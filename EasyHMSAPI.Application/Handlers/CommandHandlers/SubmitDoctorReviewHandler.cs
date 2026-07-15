using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>
    /// Public, anonymous review submission — anyone visiting the doctor's page can post a
    /// rating, no login/verified-appointment check (accepted product trade-off). Comment is
    /// optional: a quick "tap a star" rating (from the doctor page or right after a NexEagle
    /// booking) submits with no comment; one can be attached afterward via
    /// UpdateReviewCommentHandler, using the ReviewId this call returns. Goes live immediately;
    /// a hospital admin can hide it afterward from Public Directory (see
    /// ModerateDoctorReviewHandler), there is no pre-publish approval queue.
    /// </summary>
    public class SubmitDoctorReviewHandler : IRequestHandler<SubmitDoctorReviewRequestModel, SubmitDoctorReviewResponseModel>
    {
        private readonly AppDbContext _context;

        public SubmitDoctorReviewHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<SubmitDoctorReviewResponseModel> Handle(SubmitDoctorReviewRequestModel request, CancellationToken cancellationToken)
        {
            if (request.Rating < 1 || request.Rating > 5)
                return new SubmitDoctorReviewResponseModel { Success = false, Message = "Rating must be between 1 and 5." };

            // Only accept reviews for doctors genuinely visible on the public directory — same
            // rule PublicBookAppointmentHandler/GetPublicDoctorAvailabilityHandler already apply.
            var hospitalId = await PublicDirectoryHelpers.ResolvePubliclyListedHospitalIdAsync(_context, request.DoctorId, cancellationToken);
            if (hospitalId == null)
                return new SubmitDoctorReviewResponseModel { Success = false, Message = "Doctor not found." };

            var review = new DoctorReview
            {
                ReviewId = Guid.NewGuid(),
                HospitalId = hospitalId.Value,
                DoctorId = request.DoctorId,
                AuthorName = string.IsNullOrWhiteSpace(request.AuthorName) ? null : request.AuthorName.Trim(),
                Rating = request.Rating,
                Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
                HelpfulCount = 0,
                IsHidden = false,
                SubmittedIp = request.IpAddress,
                CreatedAt = DateTime.UtcNow,
            };
            _context.DoctorReviews.Add(review);
            await _context.SaveChangesAsync(cancellationToken);

            return new SubmitDoctorReviewResponseModel { Success = true, Message = "Review submitted.", ReviewId = review.ReviewId };
        }
    }
}
