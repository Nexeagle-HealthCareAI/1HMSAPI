using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class BillingChargeCatalog
    {
        [Key]
        public Guid ChargeItemId { get; set; }
        public Guid HospitalId { get; set; }
        public string? DisplayName { get; set; }
        public string? VisitType { get; set; }
        public decimal DefaultRate { get; set; }
        public decimal? DefaultDiscountPercent { get; set; }
        public decimal DefaultQty { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
