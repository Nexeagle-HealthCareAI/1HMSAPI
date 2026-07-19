using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

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
    /// When PatientMobile is provided (post-booking rating flow only), a second submission
    /// with the same number for the same doctor is rejected — a soft guard, not real identity
    /// verification, since that number is never OTP-checked (see SubmittedMobileHash on the
    /// entity). The primary defense against accidental double-rating is client-side
    /// (localStorage), this is only a server-side backstop for that one flow.
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

            var mobileHash = NormalizeAndHashMobile(request.PatientMobile);
            if (mobileHash != null)
            {
                var alreadyRated = await _context.DoctorReviews
                    .AnyAsync(r => r.DoctorId == request.DoctorId && r.SubmittedMobileHash == mobileHash, cancellationToken);
                if (alreadyRated)
                    return new SubmitDoctorReviewResponseModel { Success = false, Message = "You've already rated this doctor." };
            }

            var review = new DoctorReview
            {
                ReviewId = Guid.NewGuid(),
                HospitalId = hospitalId.Value,
                DoctorId = request.DoctorId,
                AuthorName = string.IsNullOrWhiteSpace(request.AuthorName) ? null : TextSanitizer.StripInvalidSurrogates(request.AuthorName.Trim()),
                Rating = request.Rating,
                Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : TextSanitizer.StripInvalidSurrogates(request.Comment.Trim()),
                HelpfulCount = 0,
                IsHidden = false,
                SubmittedIp = request.IpAddress,
                SubmittedMobileHash = mobileHash,
                CreatedAt = DateTime.UtcNow,
            };
            _context.DoctorReviews.Add(review);
            await _context.SaveChangesAsync(cancellationToken);

            return new SubmitDoctorReviewResponseModel { Success = true, Message = "Review submitted.", ReviewId = review.ReviewId };
        }

        // Keeps only the last 10 digits so "+91 9876543210" and "9876543210" hash identically
        // regardless of whether a country code was typed.
        private static string? NormalizeAndHashMobile(string? mobile)
        {
            if (string.IsNullOrWhiteSpace(mobile)) return null;
            var digits = new string(mobile.Where(char.IsDigit).ToArray());
            if (digits.Length == 0) return null;
            var normalized = digits.Length > 10 ? digits[^10..] : digits;
            return ApiKeyHasher.Hash(normalized);
        }
    }
}
