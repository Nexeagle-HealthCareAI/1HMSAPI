using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class InvitationMapUserHandler : IRequestHandler<InvitationMapUserRequestModel, InvitationMapUserResponseModel>
    {
        private readonly AppDbContext _context;
        public InvitationMapUserHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<InvitationMapUserResponseModel> Handle(InvitationMapUserRequestModel request, CancellationToken cancellationToken)
        {
            var resp = new InvitationMapUserResponseModel { InvitationId = request.InvitationId, UserId = request.UserId };

            var invitation = await _context.UserInvitations.FirstOrDefaultAsync(i => i.InvitationID == request.InvitationId, cancellationToken);
            if (invitation == null)
            {
                resp.Success = false;
                resp.Message = "Invitation not found";
                return resp;
            }

            try
            {
                var existing = await _context.HospitalUsers.FirstOrDefaultAsync(hu => hu.HospitalID == invitation.HospitalID && hu.UserID == request.UserId, cancellationToken);
                if (existing == null)
                {
                    await _context.HospitalUsers.AddAsync(new Domain.Entities.HospitalUser
                    {
                        HospitalUserID = Guid.NewGuid(),
                        HospitalID = invitation.HospitalID,
                        UserID = request.UserId,
                        IsPrimary = false,
                        CreatedAt = DateTime.UtcNow
                    }, cancellationToken);
                    resp.CreatedHospitalUserLink = true;
                }

                // Update UserRoles: set HospitalID in Role if not set
                var userRole = await _context.UserRoles
                    .Include(ur => ur.Role)
                    .FirstOrDefaultAsync(ur => ur.UserID == request.UserId, cancellationToken);
                if (userRole != null && userRole.Role != null)
                {
                    if (userRole.Role.HospitalID == null || userRole.Role.HospitalID == Guid.Empty)
                    {
                        userRole.Role.HospitalID = invitation.HospitalID;
                    }
                }

                invitation.Status = "Accepted";
                invitation.AcceptedAt = DateTime.UtcNow;
                //_context.UserInvitations.Update(invitation);

                await _context.SaveChangesAsync(cancellationToken);

                resp.Success = true;
                resp.Message = "User mapped to hospital and invitation updated.";
                resp.HospitalId = invitation.HospitalID;
                resp.InvitationStatus = invitation.Status;
                return resp;
            }
            catch (Exception ex)
            {
                resp.Success = false;
                resp.Message = ex.Message;
                return resp;
            }
        }
    }
}
