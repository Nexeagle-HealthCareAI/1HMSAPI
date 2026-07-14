using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// Links an admission to its "Referred by" (source + referrer) for a span of time. Mirrors
    /// AdmissionDoctorAssignment's ACTIVE/REPLACED shape. Admission.ReferralSource/ReferralName/
    /// ReferredByReferrerId remain the live fields every other consumer reads -- this table is the
    /// audit trail alongside them, kept in sync by AdmissionReferrerAssignmentHelper. Only covers the
    /// SELF/DOCTOR/OTHER (Referrer-master) branch -- the separate HOSPITAL referring-facility capture
    /// (ReferringFacilityName/Type/Contact) isn't a single-entity reassignment and isn't tracked here.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [Table("AdmissionReferrerAssignment")]
    public class AdmissionReferrerAssignment
    {
        [Key]
        public Guid AssignmentId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid AdmissionId { get; set; }

        public string ReferralSource { get; set; } = null!;   // SELF / DOCTOR / OTHER
        public Guid? ReferrerId { get; set; }                  // FK to Referrer master; null for SELF
        public string? ReferrerName { get; set; }               // snapshot -- survives later Referrer edits
        public string? ReferrerType { get; set; }               // DOCTOR / AGENT / REFERRER snapshot

        public DateTime AssignedAt { get; set; }
        public string? AssignedBy { get; set; }
        public DateTime? UnassignedAt { get; set; }
        public string? UnassignedBy { get; set; }

        public string StatusCode { get; set; } = "ACTIVE";   // ACTIVE / REPLACED
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
