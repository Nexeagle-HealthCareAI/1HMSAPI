using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetActiveAdmissionsRequestModel : IRequest<GetActiveAdmissionsResponseModel>
    {
        public Guid HospitalId { get; set; }
    }
}
