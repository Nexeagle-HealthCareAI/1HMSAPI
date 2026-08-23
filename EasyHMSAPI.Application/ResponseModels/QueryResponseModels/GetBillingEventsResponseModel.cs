using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetBillingEventsResponseModel
    {
        public bool? Success { get; set; }
        public string? Message { get; set; }
        public GetBillingEventsData? Data { get; set; }
    }

    public class GetBillingEventsData
    {
        public decimal TotalBilledAmount { get; set; }
        public decimal AmountReceived { get; set; }
        public decimal NetBalance { get; set; }
        public CurrentInvoiceInfo? CurrentInvoice { get; set; }
        public List<InvoiceSummary>? Invoices { get; set; }
        public List<BillingChargeDetail>? Charges { get; set; }
        public List<BillingPaymentDetail>? Payments { get; set; }
    }

    // Every invoice ever issued for the encounter (draft, finalized, cancelled), newest first --
    // lets the ledger show invoice history instead of only the single current one, and lets a
    // specific past invoice be targeted (e.g. for delete) instead of only "the current invoice."
    public class InvoiceSummary
    {
        public Guid InvoiceId { get; set; }
        public string? InvoiceNo { get; set; }
        public DateTime InvoiceDate { get; set; }
        public string? StatusCode { get; set; }
        public decimal? NetAmount { get; set; }
    }

    public class CurrentInvoiceInfo
    {
        public Guid InvoiceId { get; set; }
        public string? InvoiceNo { get; set; }
        public string? StatusCode { get; set; }
        public DateTime InvoiceDate { get; set; }
        public DateTime? FinalizedAt { get; set; }
        public string? FinalizedBy { get; set; }
        public decimal? GrossAmount { get; set; }
        public decimal? DiscountAmount { get; set; }
        public decimal? NetAmount { get; set; }

        // GST roll-up
        public decimal? TaxableAmount { get; set; }
        public decimal CgstAmount { get; set; }
        public decimal SgstAmount { get; set; }
        public decimal IgstAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public string? BuyerGstin { get; set; }
        public string? PlaceOfSupplyStateCode { get; set; }

        public bool? IsReopened { get; set; }
        public string? ReopenedReason { get; set; }
    }

    public class BillingChargeDetail
    {
        public Guid ChargeEventId { get; set; }
        public DateTime CreatedDateTime { get; set; }
        public string? DisplayName { get; set; }
        public string? CategoryCode { get; set; }
        public string? SourceModule { get; set; }
        public decimal Rate { get; set; }
        public decimal Qty { get; set; }
        public decimal GrossAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal NetAmount { get; set; }

        // GST snapshot
        public string? HsnSacCode { get; set; }
        public decimal? GstRate { get; set; }
        public decimal? TaxableAmount { get; set; }
        public decimal CgstAmount { get; set; }
        public decimal SgstAmount { get; set; }
        public decimal IgstAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public bool IsTaxInclusive { get; set; }
        public bool IsInterState { get; set; }

        public DateTime ServiceDate { get; set; }

        public string? StatusCode { get; set; }
        public bool IsInvoiced { get; set; }
    }

    public class BillingPaymentDetail
    {
        public Guid PaymentId { get; set; }
        public DateTime CreatedDateTime { get; set; }
        public string? PaymentType { get; set; }
        public string? PaymentMode { get; set; }
        public string? PaymentDescription { get; set; }
        public string? ReceiptNo { get; set; }
        public decimal Amount { get; set; }
    }
}
