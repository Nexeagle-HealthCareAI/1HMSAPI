using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    /// <summary>What must be in place before M2 linking can work for this hospital.</summary>
    [ExcludeFromCodeCoverage]
    public class GetAbdmM2ReadinessRequestModel : IRequest<AbdmM2ReadinessResponseModel>
    {
        public Guid HospitalId { get; set; }
    }

    /// <summary>Recent outbound ABDM calls with the callback that answered each.</summary>
    [ExcludeFromCodeCoverage]
    public class GetAbdmExchangesRequestModel : IRequest<AbdmExchangeListResponseModel>
    {
        public Guid HospitalId { get; set; }
        public int Take { get; set; } = 100;
    }

    /// <summary>Requests ABDM made to us that answer no call of ours (discovery, consent, data).</summary>
    [ExcludeFromCodeCoverage]
    public class GetAbdmInboundRequestsRequestModel : IRequest<AbdmInboundListResponseModel>
    {
        public Guid HospitalId { get; set; }
        public int Take { get; set; } = 100;
    }
}
