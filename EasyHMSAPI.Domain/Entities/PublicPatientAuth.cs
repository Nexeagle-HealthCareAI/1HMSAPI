using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    // WhatsApp-OTP login identity for the public "Doctor Dekho" booking portal — deliberately NOT
    // UserAuth (that's hospital-staff identity keyed by Users.UserID). Keyed by Mobile alone since
    // a patient can have PatientRegistrations rows in multiple hospitals; "my appointments" needs
    // to look across all of them for this number, not just one.
    [ExcludeFromCodeCoverage]
    [Table("PublicPatientAuth")]
    public class PublicPatientAuth
    {
        [Key]
        [MaxLength(20)]
        public string Mobile { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? Otp { get; set; }
        public DateTime? OtpSentAt { get; set; }
        public DateTime? OtpExpireAt { get; set; }
        public bool IsOtpUsed { get; set; }
        public int FailedAttempts { get; set; }
        public bool IsLocked { get; set; }

        public int OtpSendCount { get; set; }
        public DateTime? OtpWindowStartAt { get; set; }

        public int SessionEpoch { get; set; }

        // Set only in PatientOtpVerifyHandler's success branch — UpdatedAt above is touched on both
        // success and failure, so it can't answer "when did this number last actually log in".
        public DateTime? LastLoginAt { get; set; }
        public int LoginCount { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
