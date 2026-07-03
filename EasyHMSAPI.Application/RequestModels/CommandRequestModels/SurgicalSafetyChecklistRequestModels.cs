using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Items keyed by IpdConstants.WhoChecklistItems.SignIn/.TimeOut/.SignOut item Key. Not
    // DB-enforced — the handler stores whatever dictionary is posted, soft validation only.
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
}
