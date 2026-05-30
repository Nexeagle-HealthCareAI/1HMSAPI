using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class CreateDraftInvoiceResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public CreateDraftInvoiceData? Data { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class CreateDraftInvoiceData
    {
        public Guid InvoiceId { get; set; }
        public string? InvoiceNo { get; set; }
        public Guid EncounterId { get; set; }
        public int LinkedChargeCount { get; set; }
        public decimal GrossAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal NetAmount { get; set; }

        public decimal TaxableAmount { get; set; }
        public decimal CgstAmount { get; set; }
        public decimal SgstAmount { get; set; }
        public decimal IgstAmount { get; set; }
        public decimal TaxAmount { get; set; }

        public bool WasReused { get; set; }
    }
}
