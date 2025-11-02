using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class RescheduleAppointmentRequestModel : IRequest<RescheduleAppointmentResponseModel>
    {
        [Required]
        public Guid AppointmentId { get; set; }

        [Required]
        public string? PatientId { get; set; }

        public Guid? ToDoctorId { get; set; }

        [Required]
        public DateTime ToApptDate { get; set; }

        public DateTime? ToStartAt { get; set; }

        public string? Reason { get; set; }

        [Required]
        public int ExpectVersion { get; set; }
    }
}
