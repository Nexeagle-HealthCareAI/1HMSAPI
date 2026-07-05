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
        public decimal ReceivedQty { get; set; }
        public decimal RemainingQty { get; set; }
        public string Status { get; set; } = null!;
    }
}
