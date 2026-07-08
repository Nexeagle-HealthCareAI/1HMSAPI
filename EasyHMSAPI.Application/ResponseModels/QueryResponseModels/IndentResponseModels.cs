using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetIndentsResponseModel
    {
        public List<IndentDataModel> Indents { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class IndentDataModel
    {
        public Guid IndentId { get; set; }
        public string IndentNumber { get; set; } = null!;
        public Guid RequestingStoreId { get; set; }
        public string? RequestingStoreName { get; set; }
        public Guid? TargetStoreId { get; set; }
        public string? TargetStoreName { get; set; }
        public string Status { get; set; } = null!;
        public bool IsSystemGenerated { get; set; }
        public string? RequestedBy { get; set; }
        public DateTime RequestedAt { get; set; }
        public int LineCount { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GetIndentDetailResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public IndentDataModel? Indent { get; set; }
        public List<IndentLineDataModel> Lines { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class IndentLineDataModel
    {
        public Guid IndentLineId { get; set; }
        public Guid InventoryItemId { get; set; }
        public string ItemName { get; set; } = null!;
        public string Unit { get; set; } = null!;
        public decimal Qty { get; set; }
        public decimal IssuedQty { get; set; }
        public string? Notes { get; set; }
    }
}
