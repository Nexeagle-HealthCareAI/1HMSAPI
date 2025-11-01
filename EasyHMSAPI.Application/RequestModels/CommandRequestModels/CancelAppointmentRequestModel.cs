using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.ComponentModel.DataAnnotations;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    public class CancelAppointmentRequestModel : IRequest<CancelAppointmentResponseModel>
    {
        [Required]
        public Guid AppointmentId { get; set; }

        [Required]
        public string? PatientId { get; set; }
    }
}
