using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class CreatePurchaseOrderResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid PurchaseOrderId { get; set; }
        public string? PoNumber { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class PurchaseOrderActionResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
    }
}
