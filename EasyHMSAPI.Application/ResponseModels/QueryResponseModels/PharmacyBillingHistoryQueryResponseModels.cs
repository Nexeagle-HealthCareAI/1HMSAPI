using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetPharmacyBillingHistoryResponseModel
    {
        public List<PharmacyBillRow> Bills { get; set; } = new();
        // Sum of NetAmount across bills -- gross billed, unaffected by returns (kept for
        // backward-compat with existing callers).
        public decimal TotalAmount { get; set; }
        public decimal TotalReturnedAmount { get; set; }
        // TotalAmount - TotalReturnedAmount -- what the pharmacist/manager actually cares about
        // when reconciling "how much did we really sell today."
        public decimal NetSalesAmount { get; set; }
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
        // Gross amount actually charged -- unaffected by any later return (the underlying
        // BillingChargeEvent rows are never adjusted; see CreatePharmacyReturnHandler).
        public decimal NetAmount { get; set; }
        // Sum of PharmacyReturn.TotalRefundAmount for this invoice. Was previously invisible here
        // entirely, so a returned sale still showed its full original NetAmount as if nothing had
        // been refunded -- net sales were silently overstated by every processed return.
        public decimal ReturnedAmount { get; set; }
        public string? PaymentMode { get; set; }
        public string? ProcessedBy { get; set; }
        public string? StatusCode { get; set; }
    }
}
