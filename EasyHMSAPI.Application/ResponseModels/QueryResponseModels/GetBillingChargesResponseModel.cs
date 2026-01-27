using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetBillingChargesResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<BillingChargeItemDataModel>? Data { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class BillingChargeItemDataModel
    {
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
