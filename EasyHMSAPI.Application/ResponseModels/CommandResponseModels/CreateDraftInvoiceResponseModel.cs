using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class CreateDraftInvoiceResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public CreateDraftInvoiceData? Data { get; set; }
        // True when an explicit discount request would have reduced NetAmount below what's
        // already been collected — held as a PENDING CreditApproval instead of applied.
        public bool PendingApproval { get; set; }
        public Guid? CreditApprovalId { get; set; }
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
        public bool IsBackdated { get; set; }

        // Set only when a backdated InvoiceDate falls in a different Indian financial year
        // (April-March) than today -- NumberSeries.CurrentValue is a flat per-hospital counter,
        // never reset per financial year, so the printed number won't look locally sequential for
        // its stated year. Surfaced so the frontend can warn instead of silently hiding the gap.
        public string? NumberingCaveat { get; set; }
    }
}
