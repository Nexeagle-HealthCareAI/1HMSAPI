using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetAlertCountsRequestModel : IRequest<GetAlertCountsResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid? AudienceUserId { get; set; }
        public string? Role { get; set; }
    }
}
