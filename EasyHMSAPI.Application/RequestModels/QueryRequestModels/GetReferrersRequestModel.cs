using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetReferrersRequestModel : IRequest<GetReferrersResponseModel>
    {
        public Guid HospitalId { get; set; }
        public bool ActiveOnly { get; set; } = true;
        public string? Search { get; set; }
    }
}
