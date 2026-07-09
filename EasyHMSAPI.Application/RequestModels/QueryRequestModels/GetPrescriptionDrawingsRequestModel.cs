using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetPrescriptionDrawingsRequestModel : IRequest<GetPrescriptionDrawingsResponseModel>
    {
        public Guid AppointmentId { get; set; }
        public string? PatientId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid DoctorId { get; set; }
    }
}
