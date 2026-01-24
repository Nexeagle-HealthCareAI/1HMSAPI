using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class BillingChargeCatalog
    {
        public Guid ChargeItemId { get; set; }
        public Guid HospitalId { get; set; }
        public string? DisplayName { get; set; }
        public string? VisitType { get; set; }
        public decimal DefaultRate { get; set; }
        public decimal DefaultDiscountPercent { get; set; }
        public int DefaultQty { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
