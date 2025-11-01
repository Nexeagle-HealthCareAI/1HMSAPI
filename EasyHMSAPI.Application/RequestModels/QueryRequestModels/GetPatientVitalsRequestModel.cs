using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    public class GetPatientVitalsRequestModel : IRequest<PatientVitalsResponseModel>
    {
        public Guid AppointmentId { get; set; }
        public string? PatientId { get; set; }
    }
}
