using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class UpdateChargeEventResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public UpdateChargeEventData? Data { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class UpdateChargeEventData
    {
        public ChargeEventDetail? Charge { get; set; }

        // Recomputed parent-invoice totals (Guid.Empty / 0 when the charge isn't linked to an
        // invoice yet — a charge can be edited before CreateDraftInvoice ever runs).
        public Guid InvoiceId { get; set; }
        public decimal InvoiceGrossAmount { get; set; }
        public decimal InvoiceDiscountAmount { get; set; }
        public decimal InvoiceNetAmount { get; set; }
    }
}
