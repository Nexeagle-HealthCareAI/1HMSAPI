using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    public class GetPersonalizedDataRequestModel : IRequest<List<GetPersonalizedDataResponseModel>>
    {
        public Guid HospitalId { get; set; }
        public Guid DoctorId { get; set; }
        public string LookupType { get; set; } = string.Empty;
    }
}
