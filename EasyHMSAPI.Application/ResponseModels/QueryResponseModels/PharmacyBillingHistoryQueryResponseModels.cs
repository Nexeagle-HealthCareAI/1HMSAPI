using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetPharmacyBillingHistoryResponseModel
    {
        public List<PharmacyBillRow> Bills { get; set; } = new();
        public decimal TotalAmount { get; set; }
        public int TotalBills { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class PharmacyBillRow
    {
        public Guid InvoiceId { get; set; }
        public string? InvoiceNo { get; set; }
        public DateTime InvoiceDate { get; set; }
        public string? PatientId { get; set; }
        public string? PatientName { get; set; }
        public string SourceModule { get; set; } = null!;   // PHARMACY_COUNTER / PHARMACY_IPD
        public int ItemCount { get; set; }
        public decimal TotalQty { get; set; }
        public decimal NetAmount { get; set; }
        public string? PaymentMode { get; set; }
        public string? ProcessedBy { get; set; }
        public string? StatusCode { get; set; }
    }
}
