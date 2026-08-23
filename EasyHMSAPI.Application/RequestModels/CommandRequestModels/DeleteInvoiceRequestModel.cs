using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Manually deletes (soft-cancels) an invoice regardless of its current status -- DRAFT or
    // FINALIZED. Every charge that was on it is voided (not just unlinked) and any payment money
    // already allocated to it becomes unallocated again, available for a future invoice on this
    // encounter. The invoice row itself is never hard-deleted, only marked CANCELLED, preserving
    // the audit trail the same way charge/payment voids already do.
    [ExcludeFromCodeCoverage]
    public class DeleteInvoiceRequestModel : IRequest<DeleteInvoiceResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string? PatientId { get; set; }
        public Guid EncounterId { get; set; }

        // Which invoice on this encounter to delete. Required -- an encounter can accumulate more
        // than one BillingInvoice row over time (delete one, keep billing, a fresh draft appears
        // later), so "the invoice for this encounter" is not a safe lookup on its own.
        public Guid InvoiceId { get; set; }
        public string Reason { get; set; } = null!;
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
    }
}
