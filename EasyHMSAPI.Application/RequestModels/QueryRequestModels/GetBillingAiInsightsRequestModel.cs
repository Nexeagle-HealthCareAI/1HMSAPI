using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetBillingAiInsightsRequestModel : IRequest<GetBillingAiInsightsResponseModel>
    {
        public Guid HospitalId { get; set; }
    }
}
