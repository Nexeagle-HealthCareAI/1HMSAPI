using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class InvitationListHandler : IRequestHandler<InvitationListRequestModel, InvitationListResponseModel>
    {
        private readonly AppDbContext _context;
        public InvitationListHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<InvitationListResponseModel> Handle(InvitationListRequestModel request, CancellationToken cancellationToken)
        {
            var q = _context.UserInvitations
                .Where(i => i.HospitalID == request.HospitalId)
                .AsQueryable();

            string scope = request.Scope?.Trim().ToLowerInvariant() ?? "all";
            q = scope switch
            {
                "pending" => q.Where(i => i.Status == "Pending"),
                "accepted" => q.Where(i => i.Status == "Accepted"),
                "revoke" => q.Where(i => i.Status == "Revoked"),
                _ => q
            };

            var data = await q
                .OrderByDescending(i => i.CreatedAt)
                .Select(i => new InvitationItem
                {
                    InvitationId = i.InvitationID,
                    HospitalId = i.HospitalID,
                    RoleId = i.RoleID,
                    RoleName = i.Role.RoleName,
                    RecipientName = i.RecipientName,
                    RecipientMobile = i.RecipientMobile,
                    RecipientEmail = i.RecipientEmail,
                    Status = i.Status,
                    ExpiresAt = i.ExpiresAt,
                    AcceptedAt = i.AcceptedAt,
                    RevokedAt = i.RevokedAt,
                    CreatedAt = i.CreatedAt
                })
                .ToListAsync(cancellationToken);

            return new InvitationListResponseModel
            {
                Success = true,
                Invitations = data
            };
        }
    }
}
