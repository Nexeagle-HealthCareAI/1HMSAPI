using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    public class DoctorOverrideDeleteRequestModel : MediatR.IRequest<DoctorOverrideDeleteResponseModel>
    {
        public Guid OverrideId { get; set; }
    }
}
