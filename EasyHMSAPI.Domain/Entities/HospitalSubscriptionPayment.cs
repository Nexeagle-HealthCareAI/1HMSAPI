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

        // Set when this submission is a mid-cycle plan switch (upgrade/downgrade) from an
        // already-Active subscription: the unused days on PreviousPlanId were credited (prorated
        // off what the hospital actually paid for it) against the new plan's price, and Amount
        // above already reflects that discount. Lets CMS see/verify the breakdown before
        // approving instead of just a bare, unexplained Amount.
        public bool IsProratedSwitch { get; set; }
        public Guid? PreviousPlanId { get; set; }
        [MaxLength(200)]
        public string? PreviousPlanName { get; set; }
        public decimal? ProratedCreditAmount { get; set; }
    }
}
