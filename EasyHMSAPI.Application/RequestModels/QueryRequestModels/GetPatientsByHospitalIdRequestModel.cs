using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetPatientsByHospitalIdRequestModel : IRequest<GetPatientsByHospitalIdResponseModel>
    {
        public Guid HospitalId { get; set; }
    }
}
