using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class CompleteAppointmentRequestModel : IRequest<CompleteAppointmentResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid DoctordId { get; set; }
        public Guid AppointmentId { get; set; }
        public string? PatientId { get; set; }
    }
}
