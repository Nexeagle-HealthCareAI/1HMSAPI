using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// CTC salary structure for Track A (full-time salaried) employees.
    /// Drives the SalariedPayrollStrategy computation:
    ///   NET = Basic + HRA + DA + SpecialAllowance + MedicalAllowance + NightAllowance + OT
    ///         - PF(12% of Basic) - ESIC(0.75% of Gross) - PT(state slab) - TDS(Sec 192)
    ///
    /// Employer contributions (not deducted from employee):
    ///   EPF Employer = 12% of Basic (split: 8.33% → EPS, 3.67% → EPF)
    ///   ESIC Employer = 3.25% of Gross
    /// </summary>
    [ExcludeFromCodeCoverage]
    [Table("HrSalaryStructures")]
    public class HrSalaryStructure
    {
        [Key]
        public Guid HrSalaryStructureId { get; set; } = Guid.NewGuid();

        [Required]
        public Guid HrEmployeeId { get; set; }

        [Required]
        public DateOnly EffectiveFrom { get; set; }

        // ─── Gross CTC ────────────────────────────────────────────────────────
        [Required]
        [Column(TypeName = "decimal(12,2)")]
        public decimal MonthlyGrossCtc { get; set; }

        /// <summary>40–50% of CTC is standard practice for Indian hospitals.</summary>
        [Required]
        [Column(TypeName = "decimal(12,2)")]
        public decimal BasicSalary { get; set; }

        /// <summary>HRA: typically 20–40% of Basic.</summary>
        [Column(TypeName = "decimal(12,2)")]
        public decimal Hra { get; set; }

        [Column(TypeName = "decimal(12,2)")]
        public decimal DearnessAllowance { get; set; } = 0.00m;

        [Column(TypeName = "decimal(12,2)")]
        public decimal SpecialAllowance { get; set; } = 0.00m;

        [Column(TypeName = "decimal(12,2)")]
        public decimal MedicalAllowance { get; set; } = 0.00m;

        [Column(TypeName = "decimal(12,2)")]
        public decimal UniformAllowance { get; set; } = 0.00m;

        /// <summary>
        /// Per-night rate used to compute night shift allowance in payroll.
        /// Typically ₹200–₹500/night depending on grade.
        /// </summary>
        [Column(TypeName = "decimal(10,2)")]
        public decimal NightShiftAllowanceRate { get; set; } = 0.00m;

        // ─── Statutory Eligibility ────────────────────────────────────────────
        /// <summary>False for employees whose Basic > ₹15,000/mo (EPF threshold).</summary>
        public bool IsPfEligible { get; set; } = true;

        /// <summary>False for employees whose Gross > ₹21,000/mo (ESIC threshold).</summary>
        public bool IsEsiEligible { get; set; } = true;

        /// <summary>State-specific monthly PT slab (e.g. ₹200/mo for Bihar/Maharashtra).</summary>
        [Column(TypeName = "decimal(10,2)")]
        public decimal ProfessionalTax { get; set; } = 200.00m;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public HrEmployee HrEmployee { get; set; } = null!;
    }
}
