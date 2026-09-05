using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class PreviewBulkImportResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<string> UnrecognizedColumns { get; set; } = new();
        public List<BulkImportPreviewRow> Rows { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class BulkImportPreviewRow
    {
        public int RowIndex { get; set; }
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
        // Non-blocking — row can still be IsValid=true. Set when this row's batch number already
        // exists for the item+store, so the pharmacist can see it will top up an existing batch
        // (same expiry) or double-check for a typo (different expiry) before importing.
        public string? ExistingBatchWarning { get; set; }

        public string? StoreCode { get; set; }
        public string? ItemCode { get; set; }
        public string? ItemName { get; set; }
        // True when ItemCode doesn't exist in the catalogue yet but an ItemName was supplied --
        // BulkBatchCommandHandlers will create the medicine automatically as part of the commit.
        public bool WillCreateItem { get; set; }
        public string? BatchNumber { get; set; }
        public DateTime? ManufactureDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public decimal? UnitCost { get; set; }
        public decimal? Mrp { get; set; }
        public string? BarcodeValue { get; set; }
        public decimal ReceivedQty { get; set; }
    }
}
