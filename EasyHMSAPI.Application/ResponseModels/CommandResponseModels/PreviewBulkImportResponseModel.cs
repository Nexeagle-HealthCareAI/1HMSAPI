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

        public string? StoreCode { get; set; }
        public string? ItemCode { get; set; }
        public string? BatchNumber { get; set; }
        public DateTime? ManufactureDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public decimal? UnitCost { get; set; }
        public decimal? Mrp { get; set; }
        public string? BarcodeValue { get; set; }
        public decimal ReceivedQty { get; set; }
    }
}
