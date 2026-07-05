using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    // Per-charge breakdown of a BillingPaymentAllocation (payment→invoice amount). Lets a
    // specific charge's paid share be identified and reversed independently when that one
    // charge is cancelled, instead of the whole invoice's payments being an opaque lump sum.
    [ExcludeFromCodeCoverage]
    public class BillingPaymentAllocationCharge
    {
        public Guid AllocationChargeId { get; set; }
        public Guid AllocationId { get; set; }
        public Guid ChargeEventId { get; set; }
        public decimal Amount { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
    }
}
