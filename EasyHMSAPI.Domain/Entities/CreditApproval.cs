using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    [Table("CreditApproval")]
    public class CreditApproval
    {
        [Key]
        public Guid CreditApprovalId { get; set; }
        public Guid HospitalId { get; set; }

        public Guid EncounterId { get; set; }
        public string? PatientId { get; set; }

        public string PaymentType { get; set; } = string.Empty; // ADVANCE / REFUND / DISCOUNT / DELETE_CHARGE / DELETE_PAYMENT

        // Which specific ChargeEventId/PaymentId a DELETE_CHARGE/DELETE_PAYMENT request targets.
        // Null for ADVANCE/REFUND/DISCOUNT, which apply to the whole encounter, not one line.
        public Guid? TargetEventId { get; set; }
        public decimal RequestedAmount { get; set; }
        public string? PaymentMode { get; set; }
        public string? TransactionId { get; set; }
        public string? PaymentDescription { get; set; }

        // Positive magnitude of the credit balance that would result if this were posted as-is.
        public decimal ResultingCreditBalance { get; set; }
        public string? Reason { get; set; }

        public string? RequestedBy { get; set; }
        public Guid? RequestedByUserId { get; set; }
        public DateTime RequestedAt { get; set; }

        public string Status { get; set; } = "PENDING";

        public DateTime? DecidedAt { get; set; }
        public string? DecidedBy { get; set; }
        public Guid? DecidedByUserId { get; set; }
        public string? DecisionNote { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        [Timestamp]
        public byte[]? RowVersion { get; set; }
    }
}
