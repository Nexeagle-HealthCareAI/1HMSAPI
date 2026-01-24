using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    public class GetBillingChargesRequestModel : IRequest<GetBillingChargesResponseModel>
    {
        public Guid HospitalId { get; set; }
    }
}
