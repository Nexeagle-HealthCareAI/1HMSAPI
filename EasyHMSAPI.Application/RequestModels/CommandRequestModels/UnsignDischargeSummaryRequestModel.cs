using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class UnsignDischargeSummaryRequestModel : IRequest<UnsignDischargeSummaryResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid AdmissionId { get; set; }
        public string? LoggedInUserName { get; set; }
        // Stamped from the verified JWT by the controller, never read from the body.
        [JsonIgnore]
        public Guid? LoggedInUserId { get; set; }
        // Mandatory: why a signed, medico-legal document is being reopened. Kept in the audit trail.
        public string? Reason { get; set; }
    }
}