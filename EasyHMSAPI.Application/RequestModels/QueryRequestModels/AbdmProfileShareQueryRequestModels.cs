using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetAbdmProfileSharesRequestModel : IRequest<GetAbdmProfileSharesResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string? CounterId { get; set; }
        // "NEW" | "HANDLED" | null/"ALL"
        public string? Status { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GetAbdmFacilityRequestModel : IRequest<GetAbdmFacilityResponseModel>
    {
        public Guid HospitalId { get; set; }
    }
}
