using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetChargeMastersResponseModel
    {
        public List<ChargeMastersDataModel>? Items { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class ChargeMastersDataModel
    {
        public Guid ChargeId { get; set; }
        public string? ChargeCode { get; set; }
        public string? DisplayName { get; set; }
        public string? CategoryCode { get; set; }
        public string? SubCategoryCode { get; set; }
        public string? AppliesTo { get; set; }
        public decimal DefaultRate { get; set; }
        public decimal DefaultQty { get; set; }
        public decimal? MaxDiscountPercent { get; set; }
        public decimal? IncentiveAmount { get; set; }
        public string? HsnSacCode { get; set; }
        public bool IsTaxable { get; set; }
        public decimal? GstSlabPercent { get; set; }
        public bool TaxInclusive { get; set; }
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
        public string? Notes { get; set; }
    }
}
