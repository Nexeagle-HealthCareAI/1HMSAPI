using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Anonymous/bot-facing cancel — AppointmentId (route param, unguessable GUID) is the primary
    // gate, Mobile is the second factor cross-checked server-side against the appointment's
    // patient record. Deliberately does NOT accept PatientId/HospitalId from the caller (see
    // CancelAppointmentRequestModel, the staff-JWT equivalent, which does) — those are resolved
    // server-side from AppointmentId instead, same "never client-trusted" posture
    // PublicBookAppointmentHandler uses for HospitalId.
    [ExcludeFromCodeCoverage]
    public class PublicCancelAppointmentRequestModel : IRequest<PublicCancelAppointmentResponseModel>
    {
        [JsonIgnore]
        public Guid AppointmentId { get; set; }

        [Required]
        public string Mobile { get; set; } = string.Empty;

        public string? Reason { get; set; }
    }
}
