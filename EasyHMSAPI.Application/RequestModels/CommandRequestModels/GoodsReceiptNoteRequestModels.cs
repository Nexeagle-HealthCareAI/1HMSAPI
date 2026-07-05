using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GoodsReceiptNoteLineInput
    {
        public Guid PurchaseOrderLineId { get; set; }
        public Guid InventoryItemId { get; set; }
        public string BatchNumber { get; set; } = null!;
        public DateTime? ManufactureDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public decimal Qty { get; set; }
        public decimal Rate { get; set; }
    }

    // Per line: creates a Batch (ReceivedQty=Qty, RemainingQty=0), then nested-sends
    // RecordInventoryMovementRequestModel (RECEIVE, BatchId, StoreId=ReceivedStoreId) to bring
    // RemainingQty/StockLevel/CurrentStock up — reusing the existing INV-2 handler, not
    // duplicating its stock-mutation logic. Wrapped in an explicit transaction (same pattern as
    // IntraOpCommandHandlers.RecordIntraOpItemUsage): any line failing rolls back the whole GRN.
    [ExcludeFromCodeCoverage]
    public class CreateGoodsReceiptNoteRequestModel : IRequest<CreateGoodsReceiptNoteResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        [JsonIgnore]
        public Guid? LoggedInUserId { get; set; }

        public Guid PurchaseOrderId { get; set; }
        public Guid ReceivedStoreId { get; set; }
        public string? InvoiceNumber { get; set; }
        public DateTime? InvoiceDate { get; set; }
        public decimal? InvoiceAmount { get; set; }
        public string? Notes { get; set; }
        public List<GoodsReceiptNoteLineInput> Lines { get; set; } = new();
    }
}
