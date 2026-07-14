using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>A single timestamped CLABSI/CAUTI/VAP bundle-compliance check against an
    /// active DeviceAssignment. Insert-only log (bundles are checked every shift, not once
    /// a day). ItemsJson holds per-item compliance for the fixed item set defined by
    /// IpdConstants.CareBundleItems for the device's type; CompliantCount/TotalItems/
    /// AllCompliant are computed and trusted only server-side.</summary>
    [ExcludeFromCodeCoverage]
    [Table("DeviceCareBundleCheck")]
    public class DeviceCareBundleCheck
    {
        [Key]
        public Guid CheckId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid AdmissionId { get; set; }
        public Guid DeviceAssignmentId { get; set; }
        public string DeviceType { get; set; } = null!;

        public string ItemsJson { get; set; } = null!;
        public int CompliantCount { get; set; }
        public int TotalItems { get; set; }
        public bool AllCompliant { get; set; }

        public string? Notes { get; set; }

        public string CheckedBy { get; set; } = null!;
        public Guid? CheckedByUserId { get; set; }
        public DateTime CheckedAt { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
