using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class ActivateRapidResponseRequestModel : IRequest<ActivateRapidResponseResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        public Guid AdmissionId { get; set; }
        public string TriggerReason { get; set; } = null!;
        public int? TriggeredEwsScore { get; set; }
        public string? RespondingTeam { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class MarkRapidResponseArrivedRequestModel : IRequest<UpdateRapidResponseResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid ActivationId { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class ResolveRapidResponseRequestModel : IRequest<UpdateRapidResponseResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        public Guid ActivationId { get; set; }
        public string Outcome { get; set; } = null!;
        public string? OutcomeNotes { get; set; }
    }
}
