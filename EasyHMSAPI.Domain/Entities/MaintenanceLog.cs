using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>Insert-only maintenance/calibration/repair history for one Equipment row.</summary>
    [ExcludeFromCodeCoverage]
    public class MaintenanceLog
    {
        [Key]
        public Guid MaintenanceLogId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid EquipmentId { get; set; }

        public string ActivityType { get; set; } = null!;   // PM/BREAKDOWN/CALIBRATION/INSPECTION/REPAIR/OTHER

        public DateTime PerformedAt { get; set; }
        public string PerformedBy { get; set; } = null!;
        public Guid? PerformedByUserId { get; set; }
        public string? VendorName { get; set; }

        public decimal? Cost { get; set; }
        public string? PartsReplaced { get; set; }
        public string? Findings { get; set; }
        public string? ActionTaken { get; set; }

        public string? Outcome { get; set; }   // PASS/FAIL/NEEDS_FOLLOWUP

        public DateTime? NextDueAtOverride { get; set; }

        public string? Notes { get; set; }
        public string? Attachments { get; set; }

        public DateTime CreatedAt { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
