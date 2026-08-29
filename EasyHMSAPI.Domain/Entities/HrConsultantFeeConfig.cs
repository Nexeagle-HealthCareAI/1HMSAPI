using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// Fee configuration for Track B visiting consultants (Section 194J).
    /// Drives the ConsultantPayrollStrategy computation:
    ///   GrossFees = Retainer + (OPD Consultations × Fee × OpdSharePercent/100)
    ///             + (IPD Visits × IpdVisitFee)
    ///             + SumOf(Surgery cases × agreed package cut)
    ///   Net = GrossFees - TDS(10%) - AdminSurcharge
    ///
    /// Surgery share config is stored as JSON array:
    /// [{"packageName":"Lap Choly","consultantShare":15000}, ...]
    /// </summary>
    [ExcludeFromCodeCoverage]
    [Table("HrConsultantFeeConfigs")]
    public class HrConsultantFeeConfig
    {
        [Key]
        public Guid HrConsultantFeeConfigId { get; set; } = Guid.NewGuid();

        [Required]
        public Guid HrEmployeeId { get; set; }

        [Required]
        public DateOnly EffectiveFrom { get; set; }

        /// <summary>Fixed monthly guaranteed retainer in INR.</summary>
        [Column(TypeName = "decimal(12,2)")]
        public decimal MonthlyRetainer { get; set; } = 0.00m;

        /// <summary>
        /// Consultant's share percentage of OPD consultation fee.
        /// e.g. 60 means consultant gets 60% of OPD fee billed to patient.
        /// </summary>
        [Column(TypeName = "decimal(5,2)")]
        public decimal OpdSharePercent { get; set; } = 0.00m;

        /// <summary>Flat fee per IPD inpatient round/visit.</summary>
        [Column(TypeName = "decimal(10,2)")]
        public decimal IpdVisitFee { get; set; } = 0.00m;

        /// <summary>
        /// JSON array of surgery package cuts.
        /// Synced from ConsultantIncentiveLedger to avoid duplicate Excel calculation.
        /// Format: [{"packageName":"...", "consultantShare": 15000}]
        /// </summary>
        [Column(TypeName = "nvarchar(max)")]
        public string? SurgeryShareConfigJson { get; set; }

        /// <summary>
        /// Hospital administrative / equipment usage surcharge deducted from gross fees.
        /// e.g. ₹5,000/month for OT equipment usage.
        /// </summary>
        [Column(TypeName = "decimal(10,2)")]
        public decimal AdminSurcharge { get; set; } = 0.00m;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public HrEmployee HrEmployee { get; set; } = null!;
    }
}
