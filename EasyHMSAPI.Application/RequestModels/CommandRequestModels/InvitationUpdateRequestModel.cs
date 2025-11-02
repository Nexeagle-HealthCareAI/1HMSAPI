using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class InvitationUpdateRequestModel : IRequest<InvitationUpdateResponseModel>
    {
        public Guid InvitationId { get; set; }
        public string Scope { get; set; } = null!;
        public Guid PerformedByUserId { get; set; }
    }
}
