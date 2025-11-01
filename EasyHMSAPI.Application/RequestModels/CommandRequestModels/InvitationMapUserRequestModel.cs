using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    public class InvitationMapUserRequestModel : MediatR.IRequest<InvitationMapUserResponseModel>
    {
        public Guid InvitationId { get; set; }
        public Guid UserId { get; set; }
    }
}
