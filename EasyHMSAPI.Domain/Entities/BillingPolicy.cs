using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class BillingPolicy
    {
        [Key]
        public Guid BillingPolicyId { get; set; }
        public Guid HospitalId { get; set; }
        public string? LabPathTrigger { get; set; }
        public string? LabRadTrigger { get; set; }
        public string? PharmacyIpdTrigger { get; set; }
        public string? OpdConsultTrigger { get; set; }
        public string? IpdBedChargeMode { get; set; }

        // GST
        public string? SupplierGstin { get; set; }
        public string? PlaceOfSupplyStateCode { get; set; }
        public bool DefaultPriceIsTaxInclusive { get; set; }
        public string TaxRoundingMode { get; set; } = "ROUND"; // ROUND / FLOOR / CEIL

        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
