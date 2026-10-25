using EasyHMSAPI.Application.Common;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Emergency takeover (Vita vision, Phase 3). Public: list the nearest places, alert them, log a call tap, share the patient's number
    // with consent. Hospital (1HMS portal, Settings -> Emergency takeover): read and save the hospital's opt-in. Internal (CMS): verify
    // opt-ins, add places not on 1HMS, read the alert log.

    [ExcludeFromCodeCoverage]
    public class GetEmergencyPlacesRequestModel : IRequest<EmergencyPlacesResponseModel>
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string? Capability { get; set; }
        public int Take { get; set; } = EmergencyRules.MaxPlaces;
    }

    [ExcludeFromCodeCoverage]
    public class RaiseEmergencyAlertRequestModel : IRequest<RaiseEmergencyAlertResponseModel>
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        /// <summary>What triggered the emergency check, as the patient would understand it ("chest pain").</summary>
        public string? Reason { get; set; }
        public string? Capability { get; set; }
        public string? Channel { get; set; }
        /// <summary>The chat session: one session alerts once per emergency (30 minutes).</summary>
        public string? SessionRef { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class ShareEmergencyNumberRequestModel : IRequest<EmergencyActionResponseModel>
    {
        [JsonIgnore] public Guid EventId { get; set; }
        public string? Mobile { get; set; }
        /// <summary>Must be true: the patient ticked "Share my number so the hospital can call me".</summary>
        public bool Consent { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class LogEmergencyCallRequestModel : IRequest<EmergencyActionResponseModel>
    {
        [JsonIgnore] public Guid EventId { get; set; }
        public Guid SettingId { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GetHospitalEmergencySettingsRequestModel : IRequest<HospitalEmergencySettingsResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore] public Guid CallerUserId { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class SaveHospitalEmergencySettingsRequestModel : EmergencyPlaceInput, IRequest<HospitalEmergencySettingsResponseModel>
    {
        [JsonIgnore] public Guid HospitalId { get; set; }
        [JsonIgnore] public Guid CallerUserId { get; set; }
        public bool IsEnabled { get; set; }
        /// <summary>The admin ticked "I accept the emergency takeover terms" (needed once per terms version, before switching on).</summary>
        public bool AcceptTerms { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class ListEmergencyPlacesRequestModel : IRequest<EmergencyPlaceListResponseModel>
    {
        /// <summary>PENDING, VERIFIED, REJECTED or empty for all.</summary>
        public string? Status { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class VerifyEmergencyPlaceRequestModel : IRequest<EmergencyActionResponseModel>
    {
        [JsonIgnore] public Guid SettingId { get; set; }
        /// <summary>VERIFIED or REJECTED.</summary>
        public string? Decision { get; set; }
        public string? Note { get; set; }
        [JsonIgnore] public string? Actor { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class SaveCmsEmergencyPlaceRequestModel : EmergencyPlaceInput, IRequest<EmergencyPlaceListResponseModel>
    {
        [JsonIgnore] public Guid? SettingId { get; set; }
        public string? Name { get; set; }
        public bool IsEnabled { get; set; } = true;
        [JsonIgnore] public string? Actor { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GetEmergencyAlertLogRequestModel : IRequest<EmergencyAlertLogResponseModel>
    {
        public DateTime? FromUtc { get; set; }
        public DateTime? ToUtc { get; set; }
        public int Take { get; set; } = 100;
    }
}
