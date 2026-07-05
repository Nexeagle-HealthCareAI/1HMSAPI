using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class PurchaseOrderLineInput
    {
        public Guid InventoryItemId { get; set; }
        public decimal Qty { get; set; }
        public decimal Rate { get; set; }
    }

    // Creates a PO directly (no Indent) in DRAFT status.
    [ExcludeFromCodeCoverage]
    public class CreatePurchaseOrderRequestModel : IRequest<CreatePurchaseOrderResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        [JsonIgnore]
        public Guid? LoggedInUserId { get; set; }

        public Guid VendorId { get; set; }
        public DateTime? ExpectedDeliveryDate { get; set; }
        public string? Notes { get; set; }
        public List<PurchaseOrderLineInput> Lines { get; set; } = new();
    }

    // DRAFT -> APPROVED.
    [ExcludeFromCodeCoverage]
    public class ApprovePurchaseOrderRequestModel : IRequest<PurchaseOrderActionResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        [JsonIgnore]
        public Guid? LoggedInUserId { get; set; }
        public Guid PurchaseOrderId { get; set; }
    }

    // APPROVED -> SENT (marks that it was actually sent to the vendor).
    [ExcludeFromCodeCoverage]
    public class MarkPurchaseOrderSentRequestModel : IRequest<PurchaseOrderActionResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        public Guid PurchaseOrderId { get; set; }
    }

    // DRAFT/APPROVED/SENT -> CANCELLED.
    [ExcludeFromCodeCoverage]
    public class CancelPurchaseOrderRequestModel : IRequest<PurchaseOrderActionResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        public Guid PurchaseOrderId { get; set; }
        public string Reason { get; set; } = null!;
    }
}
