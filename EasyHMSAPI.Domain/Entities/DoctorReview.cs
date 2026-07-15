using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class DoctorReview
    {
        [Key]
        public Guid ReviewId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid DoctorId { get; set; }
        public string? AuthorName { get; set; }
        public int Rating { get; set; }
        // Optional -- a quick "tap a star" rating has no comment; one can be attached
        // afterward via UpdateReviewCommentHandler.
        public string? Comment { get; set; }
        public int HelpfulCount { get; set; }
        public bool IsHidden { get; set; }
        // Posted by the hospital admin from Public Directory, not a patient — always
        // labeled "Hospital Response" client-side, excluded from rating/count aggregates.
        public bool IsHospitalResponse { get; set; }
        public string? SubmittedIp { get; set; }
        // SHA-256 hash of the (unverified) phone number entered during a NexEagle booking —
        // used as a soft one-rating-per-doctor guard for the post-booking rating. Never real
        // identity verification (the number isn't OTP-checked), just a free defense-in-depth
        // signal since that flow already collects a phone number for booking. Null for the
        // anonymous doctor-page quick-rate flow.
        public string? SubmittedMobileHash { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public Doctor Doctor { get; set; } = null!;
    }
}
