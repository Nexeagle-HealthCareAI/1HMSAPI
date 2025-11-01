using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    public class DoctorTimeOffDeleteRequestModel : MediatR.IRequest<DoctorTimeOffDeleteResponseModel>
    {
        public Guid TimeOffId { get; set; }
    }
}
