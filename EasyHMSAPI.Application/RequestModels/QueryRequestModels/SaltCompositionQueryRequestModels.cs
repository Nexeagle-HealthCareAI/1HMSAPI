using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetMoleculesRequestModel : IRequest<GetMoleculesResponseModel>
    {
        public string? Search { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GetSaltCompositionsRequestModel : IRequest<GetSaltCompositionsResponseModel>
    {
        public string? Search { get; set; }
    }

    // Alternates for an out-of-stock item: same SaltCompositionId, different InventoryItemId, with
    // live stock in the given store — the "1-click switch" candidate list.
    [ExcludeFromCodeCoverage]
    public class GetSubstituteItemsRequestModel : IRequest<GetSubstituteItemsResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid InventoryItemId { get; set; }
        public Guid? StoreId { get; set; }
    }
}
