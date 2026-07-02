using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetInventoryItemsRequestModel : IRequest<GetInventoryItemsResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string? Category { get; set; }
        public string? Search { get; set; }
        public bool ActiveOnly { get; set; } = true;
    }
}
