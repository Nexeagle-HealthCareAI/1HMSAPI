using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// Daily attendance log.
    /// Receives punch data from biometric devices (ZKTeco / Matrix / eSSL webhook),
    /// geo-fenced mobile app punch, or manual admin override.
    /// Overtime hours are computed as max(0, TotalHoursWorked - RosteredShiftHours).
    /// </summary>
    [ExcludeFromCodeCoverage]
    [Table("HrAttendanceLogs")]
    public class HrAttendanceLog
    {
        [Key]
        public Guid HrAttendanceLogId { get; set; } = Guid.NewGuid();

        [Required]
        public Guid HrEmployeeId { get; set; }

        [Required]
        public DateOnly AttendanceDate { get; set; }

        public DateTime? PunchIn { get; set; }
        public DateTime? PunchOut { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal? TotalHoursWorked { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal OvertimeHours { get; set; } = 0.00m;

        /// <summary>BIOMETRIC | GEO_MOBILE | MANUAL_OVERRIDE</summary>
        [MaxLength(50)]
        public string PunchSource { get; set; } = "BIOMETRIC";

        /// <summary>Device ID from ZKTeco/Matrix/eSSL for audit trail.</summary>
        [MaxLength(100)]
        public string? BiometricDeviceId { get; set; }

        /// <summary>Serialised lat/lng for geo-fenced mobile punch (JSON: {lat, lng}).</summary>
        [MaxLength(100)]
        public string? GeoLocation { get; set; }

        /// <summary>PRESENT | LATE | HALF_DAY | ABSENT | ON_LEAVE</summary>
        [Required]
        [MaxLength(30)]
        public string Status { get; set; } = "PRESENT";

        [MaxLength(300)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public HrEmployee HrEmployee { get; set; } = null!;
    }
}
