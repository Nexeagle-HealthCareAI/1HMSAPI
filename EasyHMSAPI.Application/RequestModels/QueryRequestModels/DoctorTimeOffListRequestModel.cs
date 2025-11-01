using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    public class DoctorTimeOffListRequestModel : MediatR.IRequest<DoctorTimeOffListResponseModel>
    {
        public Guid DoctorId { get; set; }
    }
}
