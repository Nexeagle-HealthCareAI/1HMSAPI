using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Anonymous/bot-facing reschedule — same AppointmentId-as-secret + Mobile-as-second-factor
    // model as PublicCancelAppointmentRequestModel. No ExpectVersion field: the staff-side
    // RescheduleAppointmentRequestModel has one, but RescheduleAppointmentHandler currently
    // hard-rejects anything except literally 0 (no real optimistic-concurrency column exists on
    // Appointment) — not worth carrying that dead field into a new request shape.
    [ExcludeFromCodeCoverage]
    public class PublicRescheduleAppointmentRequestModel : IRequest<PublicRescheduleAppointmentResponseModel>
    {
        [JsonIgnore]
        public Guid AppointmentId { get; set; }

        [Required]
        public string Mobile { get; set; } = string.Empty;

        [Required]
        public DateTime ToApptDate { get; set; }

        public DateTime? ToStartAt { get; set; }

        public Guid? ToDoctorId { get; set; }

        public string? Reason { get; set; }
    }
}
