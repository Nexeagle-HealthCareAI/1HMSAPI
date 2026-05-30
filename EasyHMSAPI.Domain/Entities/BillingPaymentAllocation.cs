using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    [Table("BillingPaymentAllocation")]
    public class BillingPaymentAllocation
    {
        [Key]
        public Guid AllocationId { get; set; }
        public Guid EncounterId { get; set; }
        public Guid PaymentId { get; set; }
        public Guid InvoiceId { get; set; }
        public decimal AllocatedAmount { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
    }
}
