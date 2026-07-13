using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetAdmissionReferralCommentsRequestModel : IRequest<GetAdmissionReferralCommentsResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid ReferralId { get; set; }
    }
}
