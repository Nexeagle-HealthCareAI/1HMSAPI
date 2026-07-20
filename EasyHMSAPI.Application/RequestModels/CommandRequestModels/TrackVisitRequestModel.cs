using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Page-view beacon fired by NexEagleWebsite on every page load (see PublicController.TrackVisit).
    // Deliberately fire-and-forget from the frontend's perspective — this must never block or
    // affect page rendering, so the handler is best-effort throughout (a GeoIP miss or even a save
    // failure here should never surface as a visible error to a site visitor).
    [ExcludeFromCodeCoverage]
    public class TrackVisitRequestModel : IRequest<TrackVisitResponseModel>
    {
        public string? PagePath { get; set; }
        public string? ReferrerUrl { get; set; }
        public string? UtmSource { get; set; }
        public string? UtmMedium { get; set; }
        public string? UtmCampaign { get; set; }
        public string? UserAgent { get; set; }
        public string? SessionId { get; set; }

        // Resolved server-side (PublicController) via TrustedProxyIpResolver — never trusted from
        // the client body.
        [JsonIgnore]
        public string? IpAddress { get; set; }
    }
}
