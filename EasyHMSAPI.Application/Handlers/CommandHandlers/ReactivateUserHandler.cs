using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Enums;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    // Reverses DeactivateUserHandler: flips a Revoked user back to Active. Mobile/email
    // uniqueness is global (not scoped to active users — see UQ_Users_Mobile), so if the
    // number/email was reassigned to a different, currently-active user in the meantime,
    // reactivation is blocked with a clear conflict message rather than creating a collision.
    public class ReactivateUserHandler : IRequestHandler<ReactivateUserRequestModel, ReactivateUserResponseModel>
    {
        private readonly AppDbContext _context;
        public ReactivateUserHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ReactivateUserResponseModel> Handle(ReactivateUserRequestModel request, CancellationToken cancellationToken)
        {
            var resp = new ReactivateUserResponseModel { UserId = request.UserId, HospitalId = request.HospitalId };

            if (request.CallerUserId == Guid.Empty)
            {
                resp.Success = false;
                resp.Message = "Could not resolve the signed-in user.";
                return resp;
            }

            // The caller must be an admin who belongs to this hospital.
            var callerIsMember = await _context.HospitalUsers
                .AnyAsync(hu => hu.UserID == request.CallerUserId && hu.HospitalID == request.HospitalId, cancellationToken);
            if (!callerIsMember)
            {
                resp.Success = false;
                resp.Message = "You don't have access to this hospital.";
                return resp;
            }
            if (!await Common.CallerGuards.IsAdminAsync(_context, request.CallerUserId, cancellationToken))
            {
                resp.Success = false;
                resp.Message = "Only an administrator can reactivate a member.";
                return resp;
            }

            // The target must be a member of the same hospital.
            var targetIsMember = await _context.HospitalUsers
                .AnyAsync(hu => hu.UserID == request.UserId && hu.HospitalID == request.HospitalId, cancellationToken);
            if (!targetIsMember)
            {
                resp.Success = false;
                resp.Message = "This member is not part of your hospital.";
                return resp;
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == request.UserId, cancellationToken);
            if (user == null)
            {
                resp.Success = false;
                resp.Message = "User not found";
                return resp;
            }
            if (user.UserStatusId != (int)UserStatusEnum.Revoked)
            {
                resp.Success = false;
                resp.Message = "This member is already active.";
                return resp;
            }

            // Mobile/email uniqueness is global, not active-scoped — if either now belongs to a
            // different, currently-active user, reactivating would create a collision.
            if (!string.IsNullOrWhiteSpace(user.MobileNumber))
            {
                var mobileTaken = await _context.Users.AnyAsync(
                    u => u.UserID != user.UserID && u.MobileNumber == user.MobileNumber && u.UserStatusId == (int)UserStatusEnum.Active,
                    cancellationToken);
                if (mobileTaken)
                {
                    resp.Success = false;
                    resp.Message = "This member's mobile number is now used by a different active account. Contact support to reactivate.";
                    return resp;
                }
            }
            if (!string.IsNullOrWhiteSpace(user.Email))
            {
                var emailTaken = await _context.Users.AnyAsync(
                    u => u.UserID != user.UserID && u.Email == user.Email && u.UserStatusId == (int)UserStatusEnum.Active,
                    cancellationToken);
                if (emailTaken)
                {
                    resp.Success = false;
                    resp.Message = "This member's email is now used by a different active account. Contact support to reactivate.";
                    return resp;
                }
            }

            try
            {
                var currentDateTime = DateTime.UtcNow;
                user.UserStatusId = (int)UserStatusEnum.Active;

                var userAuth = await _context.UserAuths.FirstOrDefaultAsync(a => a.UserID == user.UserID, cancellationToken);
                if (userAuth != null)
                {
                    userAuth.IsLocked = false;
                    userAuth.UserStatusId = (int)UserStatusEnum.Active;
                    userAuth.FailedLoginAttempts = 0;
                }

                var userProfile = await _context.UserProfiles.FirstOrDefaultAsync(up => up.UserID == user.UserID, cancellationToken);
                if (userProfile != null)
                {
                    userProfile.UserStatusId = (int)UserStatusEnum.Active;
                }

                _context.UserHistories.Add(new UserHistory
                {
                    UserId = user.UserID,
                    UserStatusId = (int)UserStatusEnum.Active,
                    UpdatedBy = request.CallerUserId,
                    UpdatedDate = currentDateTime,
                });

                await _context.SaveChangesAsync(cancellationToken);

                resp.Success = true;
                resp.Message = "User access reactivated";
                return resp;
            }
            catch (Exception)
            {
                resp.Success = false;
                resp.Message = "An error occurred while reactivating the user.";
                return resp;
            }
        }
    }
}
