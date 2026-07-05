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
        public string? Manufacturer { get; set; }
        public string Category { get; set; } = null!;
        public string Unit { get; set; } = null!;
        public decimal? DefaultRate { get; set; }
        public string? HsnSacCode { get; set; }
        public decimal? GstSlabPercent { get; set; }
        public bool IsTaxable { get; set; }
        public Guid? ChargeId { get; set; }
        public decimal CurrentStock { get; set; }
        public decimal MinStockLevel { get; set; }
        public string? StoreLocation { get; set; }
        public string? ScheduleClass { get; set; }
        public bool IsLasa { get; set; }
        public bool IsHighAlert { get; set; }
        public string? StorageCondition { get; set; }
        public decimal ReorderQty { get; set; }
        public decimal? MaxStockLevel { get; set; }
        public bool IsActive { get; set; }
    }
}
