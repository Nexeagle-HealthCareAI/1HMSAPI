using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class CreateDraftInvoiceRequestModel : IRequest<CreateDraftInvoiceResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string? PatientId { get; set; }
        public Guid EncounterId { get; set; }
        public decimal? InvoiceDiscountAmount { get; set; }

        // Backdated billing: only applied when a NEW draft is created -- reusing an existing draft
        // never touches its original InvoiceDate. Null => "now", unchanged from pre-backdating
        // behavior. A past date requires BackdateReason -- see BillingBackdateGuard.
        public DateTime? InvoiceDate { get; set; }
        public string? BackdateReason { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        [JsonIgnore]
        public Guid? LoggedInUserId { get; set; }
        // Internal-only: set true when re-invoked from DecideCreditApprovalHandler after an
        // admin has already approved a discount that would dip into collected money — never
        // set by the public API surface.
        [JsonIgnore]
        public bool SkipCreditApprovalCheck { get; set; }
    }
}
