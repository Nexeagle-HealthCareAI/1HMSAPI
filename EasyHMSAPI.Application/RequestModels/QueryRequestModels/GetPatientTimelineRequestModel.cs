using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetPatientTimelineRequestModel : IRequest<GetPatientTimelineResponseModel>
    {
        public string? PatientId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid DoctorId { get; set; }
    }
}