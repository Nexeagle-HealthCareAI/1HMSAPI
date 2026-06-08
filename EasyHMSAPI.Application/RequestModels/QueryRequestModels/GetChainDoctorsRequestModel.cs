using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    /// <summary>Doctors across the caller's chain, each with the hospitals they currently work at.</summary>
    [ExcludeFromCodeCoverage]
    public class GetChainDoctorsRequestModel : IRequest<GetChainDoctorsResponseModel>
    {
        public Guid UserId { get; set; }
    }
}
