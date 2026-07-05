using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetVendorsRequestModel : IRequest<GetVendorsResponseModel>
    {
        public Guid HospitalId { get; set; }
        public bool IncludeInactive { get; set; }
    }
}
