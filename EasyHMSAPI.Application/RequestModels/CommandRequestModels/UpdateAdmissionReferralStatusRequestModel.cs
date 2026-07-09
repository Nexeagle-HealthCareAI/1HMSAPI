using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class UpdateAdmissionReferralStatusRequestModel : IRequest<UpdateAdmissionReferralStatusResponseModel>
    {
        public Guid ReferralId { get; set; }
        public string? StatusCode { get; set; }   // NOT_ADMITTED / FOLLOW_UP / PENDING
        public string? NotAdmittedReason { get; set; }
        public DateTime? FollowUpDate { get; set; }
        public string? FollowUpNotes { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
    }
}
