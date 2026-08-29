using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// Daily duty roster assignment.
    /// Unique constraint on (EmployeeId, RosterDate) prevents double-booking.
    /// The rest-period violation flag is set by UpsertHrDutyRosterHandler when
    /// an employee is assigned a Morning shift immediately after a 12-hour Night shift
    /// without the mandatory 24-hour rest gap.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [Table("HrDutyRosters")]
    public class HrDutyRoster
    {
        [Key]
        public Guid HrDutyRosterId { get; set; } = Guid.NewGuid();

        [Required]
        public Guid HospitalId { get; set; }

        [Required]
        public Guid HrEmployeeId { get; set; }

        [Required]
        public Guid HrHospitalShiftId { get; set; }

        [Required]
        public DateOnly RosterDate { get; set; }

        public bool IsOnCall { get; set; } = false;

        /// <summary>Optional ward/store assignment (FK to Stores).</summary>
        public Guid? WardId { get; set; }

        /// <summary>SCHEDULED | COMPLETED | SWAPPED | ABSENT</summary>
        [MaxLength(30)]
        public string Status { get; set; } = "SCHEDULED";

        /// <summary>
        /// Set true when this assignment violates the 24-hour rest rule after a Night shift.
        /// Supervisor can override but the flag is preserved for audit.
        /// </summary>
        public bool RestPeriodViolation { get; set; } = false;

        [MaxLength(300)]
        public string? ViolationMessage { get; set; }

        /// <summary>Note if this roster slot was created by swapping with another employee.</summary>
        public Guid? SwappedWithRosterId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? CreatedBy { get; set; }

        public Hospital Hospital { get; set; } = null!;
        public HrEmployee HrEmployee { get; set; } = null!;
        public HrHospitalShift HrHospitalShift { get; set; } = null!;
    }
}
