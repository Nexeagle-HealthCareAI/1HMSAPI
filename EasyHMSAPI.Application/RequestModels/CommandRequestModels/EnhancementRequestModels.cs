using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Records that a higher sanction has been requested from the insurer/TPA. Supersedes any
    // prior pending request — a fresh request clears an earlier, not-yet-approved one.
    [ExcludeFromCodeCoverage]
    public class RecordEnhancementRequestRequestModel : IRequest<RecordEnhancementRequestResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        public Guid AdmissionId { get; set; }
        public decimal RequestedSanctionedAmount { get; set; }
    }

    // Records the insurer/TPA's approval of a pending enhancement. ApprovedAmount lets the
    // approved figure differ from what was requested (insurers don't always sanction the full ask);
    // when omitted, the previously-requested amount stands.
    [ExcludeFromCodeCoverage]
    public class RecordEnhancementApprovalRequestModel : IRequest<RecordEnhancementApprovalResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        public Guid AdmissionId { get; set; }
        public decimal? ApprovedSanctionedAmount { get; set; }
    }
}
