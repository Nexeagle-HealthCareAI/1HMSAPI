using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetAbhaAccountsRequestModel : IRequest<GetAbhaAccountsResponseModel>
    {
        public Guid HospitalId { get; set; }
    }
}
