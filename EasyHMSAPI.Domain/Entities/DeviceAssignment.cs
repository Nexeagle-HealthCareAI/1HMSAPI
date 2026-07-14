using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>An invasive device (central line/catheter/ETT) driving CLABSI/CAUTI/VAP
    /// risk. ACTIVE/REMOVED lifecycle mirrors RestraintOrder, but unlike RestraintOrder a
    /// patient can hold multiple concurrent device types — only one ACTIVE row per
    /// (admission, device type) at a time (UX_DA_AdmissionDeviceTypeActive).</summary>
    [ExcludeFromCodeCoverage]
    [Table("DeviceAssignment")]
    public class DeviceAssignment
    {
        [Key]
        public Guid DeviceAssignmentId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid AdmissionId { get; set; }
        public Guid? EncounterId { get; set; }
        public string? PatientId { get; set; }

        public string DeviceType { get; set; } = null!;   // CENTRAL_LINE / URINARY_CATHETER / ETT

        public string? InsertionSite { get; set; }
        public string? Indication { get; set; }

        public string InsertedByDoctorName { get; set; } = null!;
        public DateTime InsertedAt { get; set; }

        public DateTime? RemovedAt { get; set; }
        public string? RemovedBy { get; set; }
        public Guid? RemovedByUserId { get; set; }
        public string? RemovalReason { get; set; }

        public string StatusCode { get; set; } = "ACTIVE";   // ACTIVE / REMOVED

        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
