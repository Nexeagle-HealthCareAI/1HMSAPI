using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class PrintBillingResponseModel
    {
        public bool? Success { get; set; }
        public string? Message { get; set; }
        public PrintBillingData? Data { get; set; }
    }

    public class PrintBillingData
    {
        public HospitalInfo? Hospital { get; set; }
        public InvoiceInfo? Invoice { get; set; }
        public List<PrintBillingChargeDetail>? Charges { get; set; }
        public List<PrintBillingPaymentDetail>? Payments { get; set; }
    }

    public class HospitalInfo
    {
        public Guid HospitalId { get; set; }
        public string? Name { get; set; }
        public string? Type { get; set; }
        public string? Email { get; set; }
        public string? Contact { get; set; }
        public string? AlternateContact { get; set; }
        public string? Website { get; set; }
        public string? Location { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Country { get; set; }
        public string? Pincode { get; set; }
        public string? GSTIN { get; set; }
        public string? PAN { get; set; }
        public string? NABH_NABL { get; set; }
    }

    public class InvoiceInfo
    {
        public string? InvoiceNo { get; set; }
        public DateTime InvoiceDate { get; set; }
        public decimal GrossAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal NetAmount { get; set; }
    }

    public class PrintBillingChargeDetail
    {
        public string? DisplayName { get; set; }
        public decimal Qty { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal GrossAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal NetAmount { get; set; }
    }

    public class PrintBillingPaymentDetail
    {
        public string? ReceiptNo { get; set; }
        public string? PaymentType { get; set; }
        public string? PaymentMode { get; set; }
        public decimal Amount { get; set; }
    }
}
