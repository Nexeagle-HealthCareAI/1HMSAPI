using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetUnifiedStockVisibilityResponseModel
    {
        public List<StoreStockSummaryRow> InventoryByStore { get; set; } = new();
        public List<BloodStockSummaryRow> BloodByStore { get; set; } = new();
        public List<CssdStockSummaryRow> CssdByStore { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class StoreStockSummaryRow
    {
        public string StoreName { get; set; } = null!;
        public int ItemCount { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class BloodStockSummaryRow
    {
        public string StoreName { get; set; } = null!;
        public string Component { get; set; } = null!;
        public string BloodGroup { get; set; } = null!;
        public string Status { get; set; } = null!;
        public int BagCount { get; set; }
        public decimal TotalVolumeMl { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class CssdStockSummaryRow
    {
        public string StoreName { get; set; } = null!;
        public string CurrentStatus { get; set; } = null!;
        public int SetCount { get; set; }
    }
}
