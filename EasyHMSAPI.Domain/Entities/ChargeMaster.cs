using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class ChargeMaster
    {
        [Key]
        public Guid ChargeId { get; set; }
        public Guid HospitalId { get; set; }
        public string? ChargeCode { get; set; }
        public string? DisplayName { get; set; }
        public string? CategoryCode { get; set; }
        public string? SubCategoryCode { get; set; }
        public string? AppliesTo { get; set; }
        public decimal DefaultRate { get; set; }
        public decimal DefaultQty { get; set; }
        public decimal? MaxDiscountPercent { get; set; }

        // Default incentive (flat INR per unit) earned when this service is billed; null/0 = none.
        public decimal? IncentiveAmount { get; set; }

        // GST
        public string? HsnSacCode { get; set; }
        public bool IsTaxable { get; set; }
        public decimal? GstSlabPercent { get; set; }  // 0 / 5 / 12 / 18 / 28
        public bool TaxInclusive { get; set; }

        // Per-item IRDAI payable/non-payable classification (real TPA claim forms carry a
        // "Non-Payable Items" annexure). Hospital-configurable, default payable.
        public bool IsIRDAIPayable { get; set; }

        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
