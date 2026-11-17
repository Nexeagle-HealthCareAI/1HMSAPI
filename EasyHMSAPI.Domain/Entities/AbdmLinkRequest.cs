using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// One user-initiated linking attempt (ABDM M2): a patient searching for their records from
    /// their ABHA app. The row is created when we answer the discovery and carries the attempt
    /// through the OTP (init) and its check (confirm). It remembers which care contexts we offered,
    /// so a later step can only ever link what this discovery found for this patient.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [Table("AbdmLinkRequest")]
    public class AbdmLinkRequest
    {
        [Key]
        public Guid LinkRequestId { get; set; }
        public Guid HospitalId { get; set; }
        /// <summary>ABDM's transaction id for the discovery; init quotes it back.</summary>
        public string TransactionId { get; set; } = string.Empty;
        public string PatientId { get; set; } = string.Empty;
        public string? AbhaAddress { get; set; }
        /// <summary>Care context reference numbers offered in on-discover, comma separated.</summary>
        public string OfferedReferences { get; set; } = string.Empty;
        /// <summary>The ones the patient chose at init, comma separated.</summary>
        public string? RequestedReferences { get; set; }
        /// <summary>Our reference for the OTP step; confirm quotes it back.</summary>
        public string? LinkReferenceNumber { get; set; }
        /// <summary>SHA-256 of the OTP salted with the link reference. The OTP itself is never stored.</summary>
        public string? OtpHash { get; set; }
        public DateTime? OtpExpiresAt { get; set; }
        public int OtpAttempts { get; set; }
        public string Status { get; set; } = AbdmLinkRequestStatus.Discovered;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public static class AbdmLinkRequestStatus
    {
        public const string Discovered = "DISCOVERED";
        public const string OtpSent = "OTP_SENT";
        public const string Linked = "LINKED";
        /// <summary>Too many wrong OTPs, or the OTP could not be sent. A new discovery starts afresh.</summary>
        public const string Closed = "CLOSED";
    }
}
