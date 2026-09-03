using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetRtvEligibleBatchesResponseModel
    {
        public List<RtvEligibleBatchRow> Batches { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class RtvEligibleBatchRow
    {
        public Guid BatchId { get; set; }
        public Guid InventoryItemId { get; set; }
        public string? ItemName { get; set; }
        public string BatchNumber { get; set; } = null!;
        public DateTime? ExpiryDate { get; set; }
        public int? DaysToExpiry { get; set; }
        public decimal RemainingQty { get; set; }
        public decimal? UnitCost { get; set; }
        public decimal EstimatedValue { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GetVendorReturnsResponseModel
    {
        public List<VendorReturnRow> Returns { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class VendorReturnRow
    {
        public Guid VendorReturnId { get; set; }
        public string ReturnNoteNo { get; set; } = null!;
        public string? VendorName { get; set; }
        public decimal TotalQty { get; set; }
        public decimal TotalValue { get; set; }
        public DateTime GeneratedAt { get; set; }
        public string? GeneratedBy { get; set; }
        public List<VendorReturnLineRow> Lines { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class VendorReturnLineRow
    {
        public string? ItemName { get; set; }
        public string? BatchNumber { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public decimal Qty { get; set; }
        public decimal UnitCost { get; set; }
        public decimal LineValue { get; set; }
    }
}
