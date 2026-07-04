using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class DecideCreditApprovalRequestModel : IRequest<DecideCreditApprovalResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid CreditApprovalId { get; set; }
        public string Decision { get; set; } = string.Empty; // APPROVED / REJECTED
        public string? DecisionNote { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        [JsonIgnore]
        public Guid? LoggedInUserId { get; set; }
    }
}
