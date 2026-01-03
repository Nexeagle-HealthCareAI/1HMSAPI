using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetPatientAnalysisRequestModel : IRequest<GetPatientAnalysisResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string? PatientId { get; set; }
    }
}
