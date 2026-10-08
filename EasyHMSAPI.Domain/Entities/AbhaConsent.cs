using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// Server-side evidence that consent was captured before an ABHA enrolment OTP was requested: who recorded it, for which hospital,
    /// which wording (code, version and a snapshot with its SHA-256), who gave it (patient or guardian), and how many OTPs it has been used for.
    /// Insert-and-update-counters only; never deleted by the application.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class AbhaConsent
    {
        [Key]
        public Guid AbhaConsentId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid GrantedByUserId { get; set; }
        public string? GrantedByName { get; set; }
        public string PurposeCode { get; set; } = "ABHA_ENROLMENT";
        public string ConsentCode { get; set; } = null!;
        public string ConsentVersion { get; set; } = null!;
        public string ConsentTextSha256 { get; set; } = null!;
        public string ConsentTextSnapshot { get; set; } = null!;
        public string GivenBy { get; set; } = "PATIENT";       // PATIENT | GUARDIAN
        public string? SubjectName { get; set; }
        public DateTime CreatedAt { get; set; }
        public int OtpRequestCount { get; set; }
        public DateTime? LastOtpRequestedAt { get; set; }
        public string? TxnId { get; set; }                      // ABDM transaction of the latest OTP request
    }
}
