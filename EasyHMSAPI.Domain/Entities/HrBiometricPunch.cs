using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// One raw scan received from a device. Attendance (HrAttendanceLog) is REBUILT from these rows,
    /// so a retried batch, an out-of-order batch, or a PIN mapped after the fact all give the same
    /// correct result, and every attendance row can be traced back to the scans behind it.
    /// Unique on (device, PIN, time): a device re-sending a batch can never double-count.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [Table("HrBiometricPunches")]
    public class HrBiometricPunch
    {
        [Key]
        public Guid HrBiometricPunchId { get; set; } = Guid.NewGuid();

        [Required]
        public Guid HospitalId { get; set; }

        [Required]
        public Guid HrBiometricDeviceId { get; set; }

        /// <summary>The user's PIN ("User ID") on the device.</summary>
        [Required]
        [MaxLength(50)]
        public string DeviceUserId { get; set; } = null!;

        /// <summary>Null while the PIN isn't mapped to an employee yet ("unmapped").</summary>
        public Guid? HrEmployeeId { get; set; }

        /// <summary>
        /// The device's own wall-clock time for the scan, exactly as sent (devices send local time with
        /// no timezone). Shift times and attendance dates are wall-clock too, so they compare directly.
        /// </summary>
        [Required]
        [Column(TypeName = "datetime2(0)")]
        public DateTime PunchTime { get; set; }

        /// <summary>Informational only (the device's check-in/out key). In/out is inferred from scan order.</summary>
        public int? StateCode { get; set; }

        /// <summary>Informational only: fingerprint / card / face / PIN verification method.</summary>
        public int? VerifyType { get; set; }

        /// <summary>The record verbatim as received, so the parser can be corrected from real device traffic.</summary>
        [MaxLength(500)]
        public string? RawLine { get; set; }

        public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    }
}
