using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>Consultant (treating-doctor) incentive sub-ledger — mirrors ReferralIncentive's
    /// shape for a different concept: who treated the patient, not who referred them.</summary>
    [ExcludeFromCodeCoverage]
    [Table("ConsultantIncentiveLedger")]
    public class ConsultantIncentiveLedger
    {
        [Key]
        public Guid ConsultantIncentiveLedgerId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid DoctorId { get; set; }
        public string PatientId { get; set; } = null!;
        public Guid? EncounterId { get; set; }
        public Guid ChargeEventId { get; set; }

        public decimal IncentiveAmount { get; set; }

        public string StatusCode { get; set; } = "ACCRUED";   // ACCRUED/PAID/CANCELLED
        public DateTime AccruedAt { get; set; }

        public DateTime? PaidAt { get; set; }
        public string? PaidBy { get; set; }
        public string? PayoutRef { get; set; }
        public decimal? TdsAmount { get; set; }

        public DateTime? CancelledAt { get; set; }
        public string? CancelledBy { get; set; }
        public string? CancelReason { get; set; }

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
