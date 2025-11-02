using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class CancelAppointmentRequestModel : IRequest<CancelAppointmentResponseModel>
    {
        [Required]
        public Guid AppointmentId { get; set; }

        [Required]
        public string? PatientId { get; set; }
    }
}
