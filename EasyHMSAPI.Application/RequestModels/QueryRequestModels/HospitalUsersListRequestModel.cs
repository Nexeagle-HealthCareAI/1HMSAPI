using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    public class HospitalUsersListRequestModel : MediatR.IRequest<HospitalUsersListResponseModel>
    {
        public Guid HospitalId { get; set; }
    }
}
