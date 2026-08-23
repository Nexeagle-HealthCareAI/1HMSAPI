using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Records one hospital-scoped marketing lead -- see AppConstants.LeadSource_*/LeadType_* for
    // valid Source/LeadType values. Fire-and-forget from both callers' perspective (NexEagleWebsite
    // and the WhatsApp bot) -- same posture as TrackEventRequestModel, never blocks or surfaces an
    // error to whoever's triggering it.
    [ExcludeFromCodeCoverage]
    public class RecordLeadRequestModel : IRequest<RecordLeadResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid? DoctorId { get; set; }
        public string Source { get; set; } = string.Empty;
        public string LeadType { get; set; } = string.Empty;
        public string? SearchQuery { get; set; }
        public string? Mobile { get; set; }
        public string? PatientName { get; set; }
        public string? SessionId { get; set; }

        // Resolved server-side (PublicController) via TrustedProxyIpResolver — never trusted from
        // the client body. Absent entirely for WhatsApp-sourced leads (no visitor IP to forward).
        [JsonIgnore]
        public string? IpAddress { get; set; }
    }
}
