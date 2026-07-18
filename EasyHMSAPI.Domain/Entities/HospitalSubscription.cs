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

        // Copied from the CMS plan catalog when a payment is approved. NULL = unlimited
        // (used for the Enterprise tier, and for hospitals that predate this column).
        public int? MaxDoctors { get; set; }
        public int? MaxBeds { get; set; }

        // Set when a CMS admin rejects a submitted payment (Status becomes "Rejected"); surfaced
        // back to the hospital admin on the EasyHMS subscription page.
        [MaxLength(500)]
        public string? RejectionReason { get; set; }
        public DateTime? RejectedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Trial/Active are only true while their end date hasn't passed — nothing flips Status to
        // "Expired" in the background, so callers must compute this instead of trusting the raw
        // column. Blocked/PendingApproval/Pending pass through unchanged.
        public string GetEffectiveStatus(DateTime utcNow)
        {
            if (Status.Equals("Trial", StringComparison.OrdinalIgnoreCase) && TrialEndDate.HasValue && TrialEndDate.Value <= utcNow)
                return "Expired";
            if (Status.Equals("Active", StringComparison.OrdinalIgnoreCase) && SubscriptionEndDate.HasValue && SubscriptionEndDate.Value <= utcNow)
                return "Expired";
            return Status;
        }
    }
}
