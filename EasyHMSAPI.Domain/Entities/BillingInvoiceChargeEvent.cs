using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    [Table("BillingInvoiceChargeEvent")]
    public class BillingInvoiceChargeEvent
    {
        [Key]
        [Column(Order = 0)]
        public Guid InvoiceId { get; set; }

        [Key]
        [Column(Order = 1)]
        public Guid ChargeEventId { get; set; }
    }
}
