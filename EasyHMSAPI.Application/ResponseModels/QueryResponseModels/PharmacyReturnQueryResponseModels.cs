using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetReturnableInvoiceLinesResponseModel
    {
        public bool Found { get; set; }
        public string? Message { get; set; }
        public Guid InvoiceId { get; set; }
        public string? InvoiceNo { get; set; }
        public Guid EncounterId { get; set; }
        public string? PatientId { get; set; }
        public DateTime InvoiceDate { get; set; }
        public List<ReturnableLineRow> Lines { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class ReturnableLineRow
    {
        public Guid ChargeEventId { get; set; }
        public Guid InventoryItemId { get; set; }
        public string? ItemName { get; set; }
        public Guid BatchId { get; set; }
        public string? BatchNumber { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public bool IsExpired { get; set; }
        public decimal DispensedQty { get; set; }
        public decimal AlreadyReturnedQty { get; set; }
        public decimal ReturnableQty { get; set; }
        public decimal UnitPrice { get; set; }
    }
}
