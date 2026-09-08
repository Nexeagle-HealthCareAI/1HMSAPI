using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>Monthly payroll batch header. One run per hospital per calendar month.</summary>
    [ExcludeFromCodeCoverage]
    [Table("HrPayrollRuns")]
    public class HrPayrollRun
    {
        [Key]
        public Guid HrPayrollRunId { get; set; } = Guid.NewGuid();

        [Required]
        public Guid HospitalId { get; set; }

        [Required]
        public int Month { get; set; }  // 1–12

        [Required]
        public int Year { get; set; }

        [Column(TypeName = "decimal(15,2)")]
        public decimal TotalGrossDisbursement { get; set; }

        [Column(TypeName = "decimal(15,2)")]
        public decimal TotalNetDisbursement { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal TotalPfDeducted { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal TotalEsiDeducted { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal TotalTdsDeducted { get; set; }

        /// <summary>DRAFT | APPROVED | DISBURSED</summary>
        [MaxLength(30)]
        public string Status { get; set; } = "DRAFT";

        public Guid? ProcessedByUserId { get; set; }
        public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;

        public Hospital Hospital { get; set; } = null!;
        public ICollection<HrPayslip> Payslips { get; set; } = new List<HrPayslip>();
    }

    /// <summary>
    /// Individual employee payslip within a payroll run.
    /// Supports both Track A (salaried) and Track B (consultant) computations.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [Table("HrPayslips")]
    public class HrPayslip
    {
        [Key]
        public Guid HrPayslipId { get; set; } = Guid.NewGuid();

        [Required]
        public Guid HrPayrollRunId { get; set; }

        [Required]
        public Guid HrEmployeeId { get; set; }

        [Required]
        [MaxLength(50)]
        public string PayslipNumber { get; set; } = null!;  // PAY-2026-08-0042

        [Required]
        [MaxLength(30)]
        public string PayrollTrack { get; set; } = null!;  // TRACK_A_SALARIED | TRACK_B_CONSULTANT

        public int TotalDaysInMonth { get; set; }

        [Column(TypeName = "decimal(4,1)")]
        public decimal PayableDays { get; set; }

        [Column(TypeName = "decimal(4,1)")]
        public decimal OvertimeDays { get; set; } = 0;

        public int NightShiftCount { get; set; } = 0;

        // ─── Track A: Earnings ────────────────────────────────────────────────
        [Column(TypeName = "decimal(12,2)")]
        public decimal BasicEarned { get; set; } = 0;

        [Column(TypeName = "decimal(12,2)")]
        public decimal HraEarned { get; set; } = 0;

        [Column(TypeName = "decimal(12,2)")]
        public decimal AllowancesEarned { get; set; } = 0;

        [Column(TypeName = "decimal(12,2)")]
        public decimal OvertimeAmount { get; set; } = 0;

        /// <summary>Night shift flat allowance (NightShiftCount × NightShiftAllowanceRate).</summary>
        [Column(TypeName = "decimal(12,2)")]
        public decimal NightAllowanceAmount { get; set; } = 0;

        /// <summary>
        /// Incentives from OPD/IPD/Surgery share pulled from ConsultantIncentiveLedger.
        /// Applies to Track B consultants and performance-incentive Track A doctors.
        /// </summary>
        [Column(TypeName = "decimal(12,2)")]
        public decimal IncentivesAmount { get; set; } = 0;

        // ─── Track B: Fee Breakdown ───────────────────────────────────────────
        [Column(TypeName = "decimal(12,2)")]
        public decimal RetainerAmount { get; set; } = 0;

        [Column(TypeName = "decimal(12,2)")]
        public decimal OpdShareAmount { get; set; } = 0;

        [Column(TypeName = "decimal(12,2)")]
        public decimal IpdVisitAmount { get; set; } = 0;

        [Column(TypeName = "decimal(12,2)")]
        public decimal SurgeryShareAmount { get; set; } = 0;

        // ─── Gross & Deductions ───────────────────────────────────────────────
        [Required]
        [Column(TypeName = "decimal(12,2)")]
        public decimal GrossEarnings { get; set; }

        /// <summary>EPF employee contribution: 12% of Basic (Track A only).</summary>
        [Column(TypeName = "decimal(10,2)")]
        public decimal PfEmployee { get; set; } = 0;

        /// <summary>ESIC employee contribution: 0.75% of Gross (Track A, Gross ≤ ₹21,000).</summary>
        [Column(TypeName = "decimal(10,2)")]
        public decimal EsiEmployee { get; set; } = 0;

        [Column(TypeName = "decimal(10,2)")]
        public decimal ProfTax { get; set; } = 0;

        /// <summary>
        /// TDS deduction:
        ///   Track A → Section 192 (income tax slab-based)
        ///   Track B → Section 194J (flat 10% of gross professional fees)
        /// </summary>
        [Column(TypeName = "decimal(10,2)")]
        public decimal TdsDeducted { get; set; } = 0;

        [Column(TypeName = "decimal(10,2)")]
        public decimal LoanInstallment { get; set; } = 0;

        [Required]
        [Column(TypeName = "decimal(12,2)")]
        public decimal TotalDeductions { get; set; }

        [Required]
        [Column(TypeName = "decimal(12,2)")]
        public decimal NetSalary { get; set; }

        // ─── Employer Contributions (informational only, not deducted) ────────
        [Column(TypeName = "decimal(10,2)")]
        public decimal PfEmployer { get; set; } = 0;  // 12% of Basic

        [Column(TypeName = "decimal(10,2)")]
        public decimal EsiEmployer { get; set; } = 0;  // 3.25% of Gross

        // ─── Distribution ─────────────────────────────────────────────────────
        [MaxLength(500)]
        public string? PdfUrl { get; set; }

        public bool IsSentWhatsapp { get; set; } = false;
        public DateTime? WhatsappSentAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public HrPayrollRun HrPayrollRun { get; set; } = null!;
        public HrEmployee HrEmployee { get; set; } = null!;
    }
}
