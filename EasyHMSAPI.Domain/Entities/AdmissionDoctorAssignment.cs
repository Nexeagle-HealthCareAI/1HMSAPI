using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// Links an admission to its admitting/primary doctor for a span of time. Filtered unique index
    /// in the DB guarantees at most one ACTIVE row per admission (concurrency backstop, mirrors
    /// BedAssignment). Admission.PrimaryDoctorId remains the live "current doctor" field every other
    /// billing/consultant-ledger/referral consumer reads -- this table is the audit trail alongside
    /// it, kept in sync by AdmissionDoctorAssignmentHelper.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [Table("AdmissionDoctorAssignment")]
    public class AdmissionDoctorAssignment
    {
        [Key]
        public Guid AssignmentId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid AdmissionId { get; set; }
        public Guid DoctorId { get; set; }

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
