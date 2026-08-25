using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetPatientVolumeForecastRequestModel : IRequest<GetPatientVolumeForecastResponseModel>
    {
        public Guid HospitalId { get; set; }
    }
}
