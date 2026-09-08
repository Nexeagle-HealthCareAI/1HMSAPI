using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// Annual leave balance ledger per employee.
    /// Leave types per Indian labour law and healthcare norms:
    ///   CL  - Casual Leave      : 12 days/year
    ///   SL  - Sick Leave        : 12 days/year
    ///   EL  - Earned/Privilege  : 15-18 days/year (encashable, carry-forward ≤ 30d)
    ///   CompOff - Compensatory  : Auto-credited for holiday/weekly-off duty (valid 60d)
    ///   Maternity              : 26 weeks fully paid
    ///   CME                    : 5-7 days for clinical conferences
    /// </summary>
    [ExcludeFromCodeCoverage]
    [Table("HrLeaveBalances")]
    public class HrLeaveBalance
    {
        [Key]
        public Guid HrLeaveBalanceId { get; set; } = Guid.NewGuid();

        [Required]
        public Guid HrEmployeeId { get; set; }

        [Required]
        public int Year { get; set; }

        [Column(TypeName = "decimal(4,1)")]
        public decimal CasualLeaveBalance { get; set; } = 12.0m;

        [Column(TypeName = "decimal(4,1)")]
        public decimal SickLeaveBalance { get; set; } = 12.0m;

        [Column(TypeName = "decimal(4,1)")]
        public decimal EarnedLeaveBalance { get; set; } = 15.0m;

        /// <summary>
        /// Comp-Off balance in days. Each day worked on a public holiday or
        /// scheduled weekly-off (as detected by AutoCreditCompOffHandler)
        /// auto-credits +1. Valid for 60 days from credit date.
        /// </summary>
        [Column(TypeName = "decimal(4,1)")]
        public decimal CompOffBalance { get; set; } = 0.0m;

        [Column(TypeName = "decimal(4,1)")]
        public decimal MaternityLeaveBalance { get; set; } = 0.0m;

        [Column(TypeName = "decimal(4,1)")]
        public decimal CmeLeaveBalance { get; set; } = 5.0m;

        // ─── Usage tracking (informational) ──────────────────────────────────
        [Column(TypeName = "decimal(4,1)")]
        public decimal CasualLeaveUsed { get; set; } = 0.0m;

        [Column(TypeName = "decimal(4,1)")]
        public decimal SickLeaveUsed { get; set; } = 0.0m;

        [Column(TypeName = "decimal(4,1)")]
        public decimal EarnedLeaveUsed { get; set; } = 0.0m;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public HrEmployee HrEmployee { get; set; } = null!;
    }
}
