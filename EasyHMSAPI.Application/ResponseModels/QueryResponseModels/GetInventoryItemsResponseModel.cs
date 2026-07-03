using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetInventoryItemsResponseModel
    {
        public List<InventoryItemDataModel> Items { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class InventoryItemDataModel
    {
        public Guid InventoryItemId { get; set; }
        public string ItemCode { get; set; } = null!;
        public string ItemName { get; set; } = null!;
        public string? GenericName { get; set; }
        public string Category { get; set; } = null!;
        public string Unit { get; set; } = null!;
        public decimal? DefaultRate { get; set; }
        public decimal CurrentStock { get; set; }
        public decimal MinStockLevel { get; set; }
        public string? StoreLocation { get; set; }
        public bool IsActive { get; set; }
    }
}
