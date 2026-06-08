using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    /// <summary>The chain owned by the caller (if any) plus its member hospitals — for chain management.</summary>
    [ExcludeFromCodeCoverage]
    public class GetMyChainRequestModel : IRequest<GetMyChainResponseModel>
    {
        public Guid UserId { get; set; }
    }
}
