using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Enums;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
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
            var resp = new InvitationMapUserResponseModel 
            { 
                InvitationId = request.InvitationId, 
                UserId = request.UserId
            };

            try
            {
                var invitation = await _context.UserInvitations
                    .FirstOrDefaultAsync(i => i.InvitationID == request.InvitationId, cancellationToken);
                if (invitation == null)
                {
                    resp.Success = false;
                    resp.Message = "Invitation not found";
                    return resp;
                }

                if (!string.IsNullOrEmpty(request.ActionType))
                {
                    if(request.ActionType.Trim().ToLower() == "invite")
                    {
                        var existing = await _context.HospitalUsers
                                .FirstOrDefaultAsync(hu => hu.HospitalID == invitation.HospitalID && hu.UserID == request.UserId, cancellationToken);

                        if (existing == null)
                        {
                            var userEmpId = await _context.UserProfiles
                                .Where(u => u.UserID == request.UserId && u.UserStatusId != (int)UserStatusEnum.Revoked)
                                .Select(u => u.EmployeeID)
                                .FirstOrDefaultAsync(cancellationToken);

                            await _context.HospitalUsers.AddAsync(new HospitalUser
                            {
                                HospitalUserID = Guid.NewGuid(),
                                HospitalID = invitation.HospitalID,
                                UserID = request.UserId,
                                IsPrimary = false,
                                EmployeeID = userEmpId ?? string.Empty,
                                CreatedAt = DateTime.UtcNow
                            }, cancellationToken);
                            resp.CreatedHospitalUserLink = true;
                        }

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

                        resp.Message = "User mapped to hospital";
                    }
                    else
                    {
                        resp.Success = false;
                        resp.Message = "Invalid action type";
                        return resp;
                    }
                }
                else
                { 
                    invitation.Status = "Accepted";
                    invitation.AcceptedAt = DateTime.UtcNow;
                    resp.Message = "Invitation accepted.";
                }

                await _context.SaveChangesAsync(cancellationToken);

                resp.Success = true;
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
