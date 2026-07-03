using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>A physical/chemical restraint order — NABH requires a physician order, a
    /// monitoring interval, and family notification. ACTIVE/RELEASED lifecycle mirrors
    /// BedAssignment; only one ACTIVE restraint per admission at a time (UX_RO_AdmissionActive).</summary>
    [ExcludeFromCodeCoverage]
    [Table("RestraintOrder")]
    public class RestraintOrder
    {
        [Key]
        public Guid RestraintOrderId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid AdmissionId { get; set; }
        public Guid? EncounterId { get; set; }
        public string? PatientId { get; set; }

        public string RestraintType { get; set; } = null!;
        public string Reason { get; set; } = null!;

        public Guid? OrderedByDoctorId { get; set; }
        public string OrderedByDoctorName { get; set; } = null!;
        public DateTime OrderedAt { get; set; }

        public DateTime StartedAt { get; set; }
        public string? StartedBy { get; set; }
        public Guid? StartedByUserId { get; set; }

        public int MonitoringIntervalMins { get; set; } = 30;

        public bool FamilyNotified { get; set; }
        public DateTime? FamilyNotifiedAt { get; set; }
        public string? FamilyNotificationNotes { get; set; }
        public Guid? RelatedConsentRecordId { get; set; }

        public DateTime? ReleasedAt { get; set; }
        public string? ReleasedBy { get; set; }
        public Guid? ReleasedByUserId { get; set; }
        public string? ReleaseReason { get; set; }

        public string StatusCode { get; set; } = "ACTIVE";   // ACTIVE / RELEASED

        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
