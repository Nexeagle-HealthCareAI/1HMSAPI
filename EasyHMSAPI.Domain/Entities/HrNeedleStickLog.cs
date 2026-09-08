using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// Needle-stick injury and occupational exposure incident log.
    /// Required for NABL / infection control compliance audits.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [Table("HrNeedleStickLogs")]
    public class HrNeedleStickLog
    {
        [Key]
        public Guid HrNeedleStickLogId { get; set; } = Guid.NewGuid();

        [Required]
        public Guid HrEmployeeId { get; set; }

        [Required]
        public DateTime IncidentDateTime { get; set; }

        /// <summary>"HIV+", "HBsAg+", "HCV+", "Unknown", "Negative"</summary>
        [MaxLength(50)]
        public string? SourcePatientStatus { get; set; }

        /// <summary>Whether Post-Exposure Prophylaxis (PEP) protocol was initiated.</summary>
        public bool PepStarted { get; set; } = false;

        public DateTime? PepStartDate { get; set; }

        [MaxLength(100)]
        public string ReportedBy { get; set; } = null!;

        [MaxLength(500)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public HrEmployee HrEmployee { get; set; } = null!;
    }
}
