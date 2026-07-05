using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetPurchaseOrdersResponseModel
    {
        public List<PurchaseOrderDataModel> PurchaseOrders { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class PurchaseOrderDataModel
    {
        public Guid PurchaseOrderId { get; set; }
        public string PoNumber { get; set; } = null!;
        public Guid VendorId { get; set; }
        public string? VendorName { get; set; }
        public Guid? IndentId { get; set; }
        public string Status { get; set; } = null!;
        public DateTime OrderedAt { get; set; }
        public DateTime? ExpectedDeliveryDate { get; set; }
        public int LineCount { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GetPurchaseOrderDetailResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public PurchaseOrderDataModel? PurchaseOrder { get; set; }
        public List<PurchaseOrderLineDataModel> Lines { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class PurchaseOrderLineDataModel
    {
        public Guid PurchaseOrderLineId { get; set; }
        public Guid InventoryItemId { get; set; }
        public string ItemName { get; set; } = null!;
        public string Unit { get; set; } = null!;
        public decimal Qty { get; set; }
        public decimal Rate { get; set; }
        public decimal ReceivedQty { get; set; }
    }
}
