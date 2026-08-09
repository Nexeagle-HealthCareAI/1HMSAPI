using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetOrderSetsRequestModel : IRequest<GetOrderSetsResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string? Category { get; set; }
        // When false (default), only active order sets are returned -- same convention as
        // GetPackageTypesRequestModel.
        public bool IncludeInactive { get; set; }
    }
}
