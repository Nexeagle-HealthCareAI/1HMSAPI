using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class InvitationCreateRequestModel : IRequest<InvitationCreateResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid RoleId { get; set; }
        public string Name { get; set; } = null!;
        public string Mobile { get; set; } = null!;
        public string? Email { get; set; }
        public Guid InvitedByUserId { get; set; }
    }
}
