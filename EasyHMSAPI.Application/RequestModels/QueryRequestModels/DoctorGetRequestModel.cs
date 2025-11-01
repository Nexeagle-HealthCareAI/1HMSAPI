using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    public class DoctorGetRequestModel : MediatR.IRequest<DoctorGetResponseModel>
    {
        public Guid UserId { get; set; }
    }
}
