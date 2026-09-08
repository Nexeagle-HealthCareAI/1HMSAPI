using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// Hospital shift master configuration.
    /// Defines the 5 standard hospital shifts:
    ///   SFT_M  - Morning Shift   (08:00–14:00)
    ///   SFT_E  - Evening Shift   (14:00–20:00)
    ///   SFT_N  - Night Shift     (20:00–08:00, 12h)
    ///   SFT_G  - General Shift   (09:30–17:30)
    ///   SFT_CALL - On-Call/Standby (24h emergency)
    /// </summary>
    [ExcludeFromCodeCoverage]
    [Table("HrHospitalShifts")]
    public class HrHospitalShift
    {
        [Key]
        public Guid HrHospitalShiftId { get; set; } = Guid.NewGuid();

        [Required]
        public Guid HospitalId { get; set; }

        /// <summary>e.g. "SFT_M", "SFT_E", "SFT_N", "SFT_G", "SFT_CALL"</summary>
        [Required]
        [MaxLength(20)]
        public string ShiftCode { get; set; } = null!;

        [Required]
        [MaxLength(100)]
        public string ShiftName { get; set; } = null!;

        [Required]
        public TimeOnly StartTime { get; set; }

        [Required]
        public TimeOnly EndTime { get; set; }

        /// <summary>Punch-in grace period in minutes (e.g. 15 for morning, 30 for night).</summary>
        public int GracePeriodMinutes { get; set; } = 15;

        /// <summary>
        /// Clinical handover buffer in minutes before shift end.
        /// Used to enforce that a nurse does not leave before handover is complete.
        /// </summary>
        public int HandoverBufferMinutes { get; set; } = 15;

        /// <summary>
        /// Flat monetary night shift allowance paid per completed night shift.
        /// e.g. ₹200–₹500/night as configured by the hospital.
        /// </summary>
        [Column(TypeName = "decimal(10,2)")]
        public decimal NightAllowanceAmount { get; set; } = 0.00m;

        /// <summary>
        /// For SFT_CALL: the additional callout fee when a standby consultant
        /// is actually called into the hospital during standby hours.
        /// </summary>
        [Column(TypeName = "decimal(10,2)")]
        public decimal CalloutFeeAmount { get; set; } = 0.00m;

        public bool IsActive { get; set; } = true;

        /// <summary>JSON array of applicable role designations.</summary>
        [MaxLength(500)]
        public string? ApplicableRolesJson { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Hospital Hospital { get; set; } = null!;
        public ICollection<HrDutyRoster> DutyRosters { get; set; } = new List<HrDutyRoster>();
    }
}
