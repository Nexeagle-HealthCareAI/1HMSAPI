using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    [Table("Admission")]
    public class Admission
    {
        [Key]
        public Guid AdmissionId { get; set; }

        public Guid HospitalId { get; set; }
        public string PatientId { get; set; } = null!;
        // Optional — a standalone admission doesn't require a billing encounter.
        public Guid? EncounterId { get; set; }
        public Guid? PrimaryDoctorId { get; set; }

        public string AdmissionNo { get; set; } = null!;

        // EMERGENCY / ELECTIVE / DAYCARE / LAMA
        public string? AdmissionType { get; set; }
        // Referral source for MIS: SELF / DOCTOR / HOSPITAL (+ free-text name + optional Referrer link)
        public string? ReferralSource { get; set; }
        public string? ReferralName { get; set; }
        public Guid? ReferredByReferrerId { get; set; }

        // Structured "referred/transferred in from an outside facility" capture (PM-JAY referral
        // rules + referral-network analytics) — a different concept from the commission-tracking
        // ReferralSource/ReferralName/ReferredByReferrerId above.
        public string? ReferringFacilityName { get; set; }
        public string? ReferringFacilityType { get; set; }   // see IpdConstants.ReferringFacilityType
        public string? ReferringFacilityContact { get; set; }

        public DateTime AdmittedAt { get; set; }
        public string? AdmittedBy { get; set; }

        public DateTime? ExpectedDischargeAt { get; set; }

        public DateTime? DischargedAt { get; set; }
        public string? DischargedBy { get; set; }
        public string? DischargeNotes { get; set; }

        public string StatusCode { get; set; } = "ADMITTED";   // see IpdConstants.AdmissionStatus

        // Payer branch — drives the whole workflow. CASH / TPA / SCHEME (detail in AdmissionCoverage).
        public string PayerType { get; set; } = "CASH";
        // Planned deposit to collect (cash flow); actual collection runs through the billing engine.
        public decimal? DepositExpected { get; set; }
        // When true, admit opens an IPD billing Encounter so charges/day-wise bills accrue to the stay.
        public bool EnableIpdBilling { get; set; } = true;
        // Offline resync idempotency key stamped by the client (unique per hospital when present).
        public Guid? ClientRequestId { get; set; }

        public string? AdmissionReason { get; set; }
        public string? Diagnosis { get; set; }

        public DateTime? CancelledAt { get; set; }
        public string? CancelledBy { get; set; }
        public string? CancelReason { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
