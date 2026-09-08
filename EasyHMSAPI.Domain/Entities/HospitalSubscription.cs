using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class HospitalSubscription
    {
        [Key]
        public Guid HospitalSubscriptionId { get; set; }
        
        public Guid HospitalId { get; set; }
        public Hospital? Hospital { get; set; }
        
        public Guid? PlanId { get; set; }
        
        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Trial"; // Trial, Active, Expired, Blocked
        
        public DateTime? TrialStartDate { get; set; }
        public DateTime? TrialEndDate { get; set; }
        public DateTime? SubscriptionStartDate { get; set; }
        public DateTime? SubscriptionEndDate { get; set; }
        public DateTime? NextBillingDate { get; set; }
        
        public decimal? PaymentAmount { get; set; }
        
        [MaxLength(100)]
        public string? PaymentReference { get; set; }

        public DateTime? PaymentDate { get; set; }

        [MaxLength(50)]
        public string? PaymentMode { get; set; } // UPI, Bank Transfer, Cheque, Card, Cash

        // Copied from the CMS plan catalog when a payment is approved. NULL = unlimited
        // (used for the Enterprise tier, and for hospitals that predate this column).
        public int? MaxDoctors { get; set; }
        public int? MaxBeds { get; set; }

        // Set when a CMS admin rejects a submitted payment (Status becomes "Rejected"); surfaced
        // back to the hospital admin on the EasyHMS subscription page.
        [MaxLength(500)]
        public string? RejectionReason { get; set; }
        public DateTime? RejectedAt { get; set; }

        // Snapshotted by HospitalRegisterHandler when a valid referral code is entered at
        // registration. RewardKind/Value are copied from CMSDatabase's ReferralCodeType at that
        // moment so nothing needs to be re-queried later. RedeemedAt is set by CMSAPI's
        // SubscriptionApprovalController.ApprovePayment the first time this hospital's subscription
        // is activated on a Yearly plan -- NULL means the reward hasn't landed yet, and doubles as
        // the idempotency guard against re-applying it on a later renewal/approval.
        [MaxLength(30)]
        public string? ReferralCode { get; set; }
        [MaxLength(20)]
        public string? ReferralCodeRewardKind { get; set; } // 'PercentageOff' | 'ExtraMonths'
        public decimal? ReferralCodeRewardValue { get; set; }
        public DateTime? ReferralCodeRedeemedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Trial no longer auto-expires by calendar date -- replaced by IUsageLimitService's
        // monthly free-tier quota (see UsageLimitService), which HospitalAccessFilter does not
        // enforce itself (each of the 5 countable-action handlers gates its own write). A Trial
        // hospital therefore stays "Trial" indefinitely from this method's point of view; only an
        // explicit admin-set Status (Blocked/Rejected) or a genuinely lapsed PAID subscription
        // still locks a hospital out here.
        //
        // Active is only true while its end date hasn't passed — nothing flips Status to
        // "Expired" in the background, so callers must compute this instead of trusting the raw
        // column. Blocked/PendingApproval/Pending pass through unchanged.
        public string GetEffectiveStatus(DateTime utcNow)
        {
            if (Status.Equals("Active", StringComparison.OrdinalIgnoreCase) && SubscriptionEndDate.HasValue && SubscriptionEndDate.Value <= utcNow)
                return "Expired";
            return Status;
        }
    }
}
