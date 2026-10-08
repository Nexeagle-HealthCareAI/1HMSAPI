using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// Per-hospital payroll calendar policy. No row = defaults (Sunday weekly off, weekly offs and
    /// holidays both payable). Table: easyHMSDatabase create_tables_hr__payroll_settings.sql.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [Table("HrPayrollSettings")]
    public class HrPayrollSettings
    {
        [Key]
        public Guid HospitalId { get; set; }

        /// <summary>Comma-separated weekday codes (SUN,MON,...,SAT). Empty = no weekly off.</summary>
        [MaxLength(40)]
        public string WeeklyOffDays { get; set; } = "SUN";

        public bool WeeklyOffPayable { get; set; } = true;
        public bool HolidayPayable { get; set; } = true;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public Guid? UpdatedByUserId { get; set; }
    }

    /// <summary>A declared hospital holiday (one row per hospital per date).</summary>
    [ExcludeFromCodeCoverage]
    [Table("HrHolidays")]
    public class HrHoliday
    {
        [Key]
        public Guid HrHolidayId { get; set; } = Guid.NewGuid();
        public Guid HospitalId { get; set; }
        public DateOnly HolidayDate { get; set; }

        [MaxLength(120)]
        public string Name { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public Guid? CreatedByUserId { get; set; }
    }
}
