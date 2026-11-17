using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// A consent artefact ABDM notified us of. Role HIP: a patient allowed someone to receive
    /// records we hold (M2). Role HIU: a patient allowed us to receive records held elsewhere (M3).
    /// Records are only ever sent or fetched against a row here that is GRANTED and in date.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [Table("AbdmConsent")]
    public class AbdmConsent
    {
        [Key]
        public Guid ConsentRowId { get; set; }
        public Guid HospitalId { get; set; }
        public string Role { get; set; } = AbdmConsentRole.Hip;
        /// <summary>ABDM's consent artefact id.</summary>
        public string ConsentId { get; set; } = string.Empty;
        /// <summary>HIU only: the consent request this artefact came from.</summary>
        public string? ConsentRequestId { get; set; }
        public string Status { get; set; } = AbdmConsentStatus.Granted;
        public string? PatientAbhaAddress { get; set; }
        /// <summary>HIU only: our patient the request was raised for.</summary>
        public string? PatientId { get; set; }
        /// <summary>The other party: the HIU we send to (role HIP) or the HIP we fetch from (role HIU).</summary>
        public string? CounterpartyId { get; set; }
        public string? CounterpartyName { get; set; }
        public string? PurposeCode { get; set; }
        public string? PurposeText { get; set; }
        /// <summary>HI types the consent covers, comma separated.</summary>
        public string HiTypes { get; set; } = string.Empty;
        /// <summary>Care context references the consent covers, comma separated.</summary>
        public string CareContextReferences { get; set; } = string.Empty;
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        /// <summary>permission.dataEraseAt: the consent is of no use after this.</summary>
        public DateTime? ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public static class AbdmConsentRole
    {
        public const string Hip = "HIP";
        public const string Hiu = "HIU";
    }

    public static class AbdmConsentStatus
    {
        public const string Requested = "REQUESTED";
        public const string Granted = "GRANTED";
        public const string Denied = "DENIED";
        public const string Revoked = "REVOKED";
        public const string Expired = "EXPIRED";
    }

    /// <summary>One transfer of records under a consent: out of this hospital (HIP) or into it (HIU).</summary>
    [ExcludeFromCodeCoverage]
    [Table("AbdmDataTransfer")]
    public class AbdmDataTransfer
    {
        [Key]
        public Guid TransferId { get; set; }
        public Guid HospitalId { get; set; }
        public string Role { get; set; } = AbdmConsentRole.Hip;
        public string ConsentId { get; set; } = string.Empty;
        public string TransactionId { get; set; } = string.Empty;
        public string Status { get; set; } = AbdmTransferStatus.Requested;
        public int RecordCount { get; set; }
        public string? ErrorMessage { get; set; }
        /// <summary>HIU only: our private key and nonce for this transfer, needed to decrypt what
        /// arrives. Cleared as soon as the records are in.</summary>
        public string? PrivateKey { get; set; }
        public string? Nonce { get; set; }
        public DateTime RequestedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public static class AbdmTransferStatus
    {
        public const string Requested = "REQUESTED";
        public const string Transferred = "TRANSFERRED";
        public const string Failed = "FAILED";
        public const string Refused = "REFUSED";
    }

    /// <summary>HIU (M3): one record received from another facility, kept as the FHIR bundle it
    /// arrived as, until the consent says it must be erased.</summary>
    [ExcludeFromCodeCoverage]
    [Table("AbdmReceivedRecord")]
    public class AbdmReceivedRecord
    {
        [Key]
        public Guid ReceivedRecordId { get; set; }
        public Guid HospitalId { get; set; }
        public string ConsentId { get; set; } = string.Empty;
        public string TransactionId { get; set; } = string.Empty;
        public string? PatientId { get; set; }
        public string? CareContextReference { get; set; }
        public string? HiType { get; set; }
        public string? Title { get; set; }
        public string BundleJson { get; set; } = string.Empty;
        public DateTime ReceivedAt { get; set; }
        public DateTime? EraseAt { get; set; }
    }
}
