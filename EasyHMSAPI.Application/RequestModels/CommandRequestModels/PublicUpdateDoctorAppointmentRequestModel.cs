using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Anonymous/bot-facing doctor reassignment — same AppointmentId-as-secret + Mobile-as-second-
    // factor model as PublicCancelAppointmentRequestModel/PublicRescheduleAppointmentRequestModel.
    [ExcludeFromCodeCoverage]
    public class PublicUpdateDoctorAppointmentRequestModel : IRequest<PublicUpdateDoctorAppointmentResponseModel>
    {
        [JsonIgnore]
        public Guid AppointmentId { get; set; }

        [Required]
        public string Mobile { get; set; } = string.Empty;

        [Required]
        public Guid NewDoctorId { get; set; }
    }
}
