using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Items keyed by IpdConstants.WhoChecklistItems.SignIn/.TimeOut/.SignOut item Key. The handler requires
    // exactly that phase's items, every one confirmed (true), and refuses to re-record a completed phase.
    [ExcludeFromCodeCoverage]
    public class RecordSignInRequestModel : IRequest<RecordSignInResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        public Guid SurgeryCaseId { get; set; }
        public Dictionary<string, bool> Items { get; set; } = new();
        public string? Notes { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class RecordTimeOutRequestModel : IRequest<RecordTimeOutResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        public Guid SurgeryCaseId { get; set; }
        public Dictionary<string, bool> Items { get; set; } = new();
        public string? Notes { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class RecordSignOutRequestModel : IRequest<RecordSignOutResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        public Guid SurgeryCaseId { get; set; }
        public Dictionary<string, bool> Items { get; set; } = new();
        public string? Notes { get; set; }
    }

    // Correction/clarification of an already-completed phase. Appended (never replaces) to that phase's notes.
    [ExcludeFromCodeCoverage]
    public class AddChecklistAddendumRequestModel : IRequest<AddChecklistAddendumResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        public Guid SurgeryCaseId { get; set; }
        // SignIn | TimeOut | SignOut
        public string? Phase { get; set; }
        public string? Text { get; set; }
    }
}
