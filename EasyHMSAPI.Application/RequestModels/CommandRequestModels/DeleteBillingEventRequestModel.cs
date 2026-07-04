using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class DeleteBillingEventRequestModel : IRequest<DeleteBillingEventResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string? PatientId { get; set; }
        public Guid EventId { get; set; }
        public string? Type { get; set; }
        public string? Reason { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        [JsonIgnore]
        public Guid? LoggedInUserId { get; set; }
        // Set only when re-invoked from DecideCreditApprovalHandler after an admin approves —
        // bypasses the approval gate below so the actual delete can proceed.
        [JsonIgnore]
        public bool SkipCreditApprovalCheck { get; set; }
    }
}
