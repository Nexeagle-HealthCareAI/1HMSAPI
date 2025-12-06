using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GeneratePrescriptionRequestModel : IRequest<GeneratePrescriptionResponseModel>
    {
        public Guid AppointmentId { get; set; }
        public string? PatientId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid DoctorId { get; set; }
    }
}
