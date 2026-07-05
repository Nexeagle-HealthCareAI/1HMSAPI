using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetStoresRequestModel : IRequest<GetStoresResponseModel>
    {
        public Guid HospitalId { get; set; }
        public bool IncludeInactive { get; set; }
    }
}
