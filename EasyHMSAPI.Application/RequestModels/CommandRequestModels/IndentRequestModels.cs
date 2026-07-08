using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class IndentLineInput
    {
        public Guid InventoryItemId { get; set; }
        public decimal Qty { get; set; }
        public string? Notes { get; set; }
    }

    // Creates in SUBMITTED status directly (human-raised indents don't need a separate draft step);
    // IsSystemGenerated=true (reorder-triggered) creates in DRAFT so a human reviews before it's
    // formally submitted for approval.
    [ExcludeFromCodeCoverage]
    public class CreateIndentRequestModel : IRequest<CreateIndentResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        [JsonIgnore]
        public Guid? LoggedInUserId { get; set; }

        public Guid RequestingStoreId { get; set; }
        public Guid? TargetStoreId { get; set; }
        public bool IsSystemGenerated { get; set; }
        public string? Notes { get; set; }
        public List<IndentLineInput> Lines { get; set; } = new();
    }

    // DRAFT -> SUBMITTED, for a system-generated draft a human has reviewed.
    [ExcludeFromCodeCoverage]
    public class SubmitIndentRequestModel : IRequest<ApproveIndentResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        public Guid IndentId { get; set; }
    }

    // SUBMITTED -> APPROVED or REJECTED (Approve=false requires Reason).
    [ExcludeFromCodeCoverage]
    public class ApproveIndentRequestModel : IRequest<ApproveIndentResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        [JsonIgnore]
        public Guid? LoggedInUserId { get; set; }

        public Guid IndentId { get; set; }
        public bool Approve { get; set; }
        public string? Reason { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class ConvertIndentLineInput
    {
        public Guid IndentLineId { get; set; }
        public decimal Rate { get; set; }
    }

    // APPROVED -> CONVERTED_TO_PO: creates a new PurchaseOrder (DRAFT) mirroring the indent's
    // lines, priced at the caller-supplied rates (an indent itself carries no pricing).
    [ExcludeFromCodeCoverage]
    public class ConvertIndentToPoRequestModel : IRequest<ConvertIndentToPoResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        public Guid IndentId { get; set; }
        public Guid VendorId { get; set; }
        public DateTime? ExpectedDeliveryDate { get; set; }
        public List<ConvertIndentLineInput> Lines { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class IssueIndentLineInput
    {
        public Guid IndentLineId { get; set; }
        public Guid BatchId { get; set; }
        public decimal Qty { get; set; }
    }

    // SUBMITTED -> ISSUED: For internal store transfers. Deducts stock from TargetStoreId, adds to RequestingStoreId.
    [ExcludeFromCodeCoverage]
    public class IssueIndentRequestModel : IRequest<IssueIndentResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        [JsonIgnore]
        public Guid? LoggedInUserId { get; set; }

        public Guid IndentId { get; set; }
        public List<IssueIndentLineInput> Lines { get; set; } = new();
    }
}
