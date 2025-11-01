using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    public class InvitationUpdateRequestModel : IRequest<InvitationUpdateResponseModel>
    {
        public Guid InvitationId { get; set; }
        public string Scope { get; set; } = null!;
        public Guid PerformedByUserId { get; set; }
    }
}
