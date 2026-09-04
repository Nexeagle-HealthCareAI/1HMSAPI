using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetBatchesForItemResponseModel
    {
        public List<BatchDataModel> Batches { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class BatchDataModel
    {
        public Guid BatchId { get; set; }
        public Guid StoreId { get; set; }
        public string? StoreName { get; set; }
        public string BatchNumber { get; set; } = null!;
        public DateTime? ManufactureDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public decimal? UnitCost { get; set; }
        public decimal? Mrp { get; set; }
        public string? BarcodeValue { get; set; }
        public decimal ReceivedQty { get; set; }
        public decimal RemainingQty { get; set; }
        public string Status { get; set; } = null!;
        // Only populated by the hospital-wide GetAllBatches query below — every other caller of
        // this DTO already knows the item from context (it's the one they asked for).
        public Guid? InventoryItemId { get; set; }
        public string? ItemName { get; set; }
        public string? ItemCode { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GetBatchByBarcodeResponseModel
    {
        public bool Found { get; set; }
        public Guid InventoryItemId { get; set; }
        public string? ItemName { get; set; }
        public BatchDataModel? Batch { get; set; }
    }

    // Flat, hospital-wide "everything currently in stock" view — unlike GetBatchesForItem (scoped to
    // one item) or the near-expiry report (scoped to a 90-day expiry window), this is the browsable
    // list for verifying/reviewing all stock, filterable by store or a free-text search.
    [ExcludeFromCodeCoverage]
    public class GetAllBatchesResponseModel
    {
        public List<BatchDataModel> Batches { get; set; } = new();
    }
}
