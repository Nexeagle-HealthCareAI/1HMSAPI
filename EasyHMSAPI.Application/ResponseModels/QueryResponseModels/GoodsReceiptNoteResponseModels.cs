using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetGoodsReceiptNotesResponseModel
    {
        public List<GoodsReceiptNoteDataModel> GoodsReceiptNotes { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class GoodsReceiptNoteDataModel
    {
        public Guid GrnId { get; set; }
        public string GrnNumber { get; set; } = null!;
        public Guid PurchaseOrderId { get; set; }
        public string? PoNumber { get; set; }
        public Guid VendorId { get; set; }
        public string? VendorName { get; set; }
        public string? ReceivedStoreName { get; set; }
        public string? InvoiceNumber { get; set; }
        public decimal? InvoiceAmount { get; set; }
        public string MatchStatus { get; set; } = null!;
        public DateTime ReceivedAt { get; set; }
    }
}
