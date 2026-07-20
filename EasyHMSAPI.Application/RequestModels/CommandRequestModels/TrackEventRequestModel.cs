using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Generic funnel/behavior event beacon fired by NexEagleWebsite (see AppConstants.
    // AnalyticsEventType_* for valid EventType values). Deliberately fire-and-forget from the
    // frontend's perspective, same as TrackVisitRequestModel — never blocks or surfaces an error.
    [ExcludeFromCodeCoverage]
    public class TrackEventRequestModel : IRequest<TrackEventResponseModel>
    {
        public string EventType { get; set; } = string.Empty;
        public string? SessionId { get; set; }
        public string? Mobile { get; set; }
        public Guid? DoctorId { get; set; }
        public string? SpecialtyId { get; set; }
        public string? MetadataJson { get; set; }

        // Resolved server-side (PublicController) via TrustedProxyIpResolver — never trusted from
        // the client body.
        [JsonIgnore]
        public string? IpAddress { get; set; }
    }
}
