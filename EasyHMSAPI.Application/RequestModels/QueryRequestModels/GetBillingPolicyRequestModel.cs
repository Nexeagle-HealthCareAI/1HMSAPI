using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetBillingPolicyRequestModel : IRequest<GetBillingPolicyResponseModel>
    {
        public Guid HospitalId { get; set; }
    }
}
