using System.Security.Cryptography;
using System.Text;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class InvitationValidateHandler : IRequestHandler<InvitationValidateRequestModel, InvitationValidateResponseModel>
    {
        private readonly AppDbContext _context;
        public InvitationValidateHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<InvitationValidateResponseModel> Handle(InvitationValidateRequestModel request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Token))
            {
                return new InvitationValidateResponseModel { Success = false, Message = "Token is required." };
            }

            byte[] tokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(request.Token));

            var invitation = await _context.UserInvitations
                .Include(i => i.Role)
                .FirstOrDefaultAsync(i => i.TokenHash == tokenHash, cancellationToken);

            if (invitation == null)
            {
                return new InvitationValidateResponseModel { Success = false, Message = "Invalid token." };
            }

            if (invitation.Status == "Revoked" || invitation.RevokedAt != null)
            {
                return new InvitationValidateResponseModel { Success = false, Message = "Invitation has been revoked." };
            }

            if (DateTime.UtcNow > invitation.ExpiresAt)
            {
                return new InvitationValidateResponseModel { Success = false, Message = "Invitation has expired." };
            }

            if (invitation.AcceptedAt != null || invitation.Status == "Accepted")
            {
                return new InvitationValidateResponseModel { Success = false, Message = "Invitation already used." };
            }

            return new InvitationValidateResponseModel
            {
                Success = true,
                Name = invitation.RecipientName,
                RoleName = invitation.Role.RoleName,
                Email = invitation.RecipientEmail,
                Mobile = invitation.RecipientMobile
            };
        }
    }
}
