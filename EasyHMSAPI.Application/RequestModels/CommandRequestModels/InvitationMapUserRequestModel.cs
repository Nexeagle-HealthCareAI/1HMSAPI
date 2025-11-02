using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class InvitationMapUserRequestModel : MediatR.IRequest<InvitationMapUserResponseModel>
    {
        public Guid InvitationId { get; set; }
        public Guid UserId { get; set; }
    }
}
