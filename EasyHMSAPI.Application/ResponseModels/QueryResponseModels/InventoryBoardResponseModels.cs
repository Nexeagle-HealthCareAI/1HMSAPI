using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetInventoryBoardResponseModel
    {
        public List<StockOverviewRow> StockByStore { get; set; } = new();
        public List<ExpiryAlertRow> ExpiryAlerts { get; set; } = new();
        public List<ReorderAlertRow> ReorderAlerts { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class StockOverviewRow
    {
        public Guid InventoryItemId { get; set; }
        public string ItemName { get; set; } = null!;
        public string Category { get; set; } = null!;
        public string Unit { get; set; } = null!;
        public Guid StoreId { get; set; }
        public string StoreName { get; set; } = null!;
        public decimal QtyOnHand { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class ExpiryAlertRow
    {
        public Guid BatchId { get; set; }
        public Guid InventoryItemId { get; set; }
        public string ItemName { get; set; } = null!;
        public string StoreName { get; set; } = null!;
        public string BatchNumber { get; set; } = null!;
        public DateTime ExpiryDate { get; set; }
        public int DaysToExpiry { get; set; }
        public decimal RemainingQty { get; set; }
        // 90/60/30-day tier, tightest one that applies — drives the board's escalating urgency.
        public int Tier { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class ReorderAlertRow
    {
        public Guid InventoryItemId { get; set; }
        public string ItemName { get; set; } = null!;
        public string Category { get; set; } = null!;
        public string Unit { get; set; } = null!;
        public decimal CurrentStock { get; set; }
        public decimal MinStockLevel { get; set; }
        public decimal ReorderQty { get; set; }
    }
}
