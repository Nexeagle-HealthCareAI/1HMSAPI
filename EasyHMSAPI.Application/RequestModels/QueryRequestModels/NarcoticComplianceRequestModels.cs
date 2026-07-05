using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetNarcoticRegisterRequestModel : IRequest<GetNarcoticRegisterResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid? InventoryItemId { get; set; }
        public string? FormType { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GetColdChainReadingsRequestModel : IRequest<GetColdChainReadingsResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid? StoreId { get; set; }
    }
}
