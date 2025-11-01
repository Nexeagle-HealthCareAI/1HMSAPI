using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    public class DeactivateUserRequestModel : MediatR.IRequest<DeactivateUserResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid UserId { get; set; }
        public Guid PerformedByUserId { get; set; }
    }
}
