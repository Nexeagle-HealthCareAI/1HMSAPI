using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetPatientVitalsRequestModel : IRequest<PatientVitalsResponseModel>
    {
        public Guid AppointmentId { get; set; }
        public string? PatientId { get; set; }
    }
}
