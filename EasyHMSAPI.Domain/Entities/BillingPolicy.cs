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
        public bool RequirePostBeforeInvoice { get; set; }
        public decimal MaxAutoDiscountPercent { get; set; }
        public string? LabPathTrigger { get; set; }
        public string? LabRadTrigger { get; set; }
        public string? PharmecyIpdTrigger { get; set; }
        public string? OpdConsultTrigger { get; set; }
        public string? IpdBedChargeMode { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
