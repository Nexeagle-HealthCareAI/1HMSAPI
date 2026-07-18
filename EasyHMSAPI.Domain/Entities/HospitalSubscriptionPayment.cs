using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    // Append-only log of every payment submission — HospitalSubscription itself only tracks the
    // single most-recent payment, this is what backs the "Payment History" view.
    [ExcludeFromCodeCoverage]
    public class HospitalSubscriptionPayment
    {
        [Key]
        public Guid PaymentId { get; set; }

        public Guid HospitalId { get; set; }
        public Guid HospitalSubscriptionId { get; set; }

        public Guid? PlanId { get; set; }
        [MaxLength(200)]
        public string? PlanName { get; set; }

        public decimal Amount { get; set; }

        [Required]
        [MaxLength(100)]
        public string Reference { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? PaymentMode { get; set; } // UPI, Bank Transfer, Cheque, Card, Cash

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "PendingApproval"; // PendingApproval, Approved, Rejected

        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ReviewedAt { get; set; }

        [MaxLength(500)]
        public string? RejectionReason { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
