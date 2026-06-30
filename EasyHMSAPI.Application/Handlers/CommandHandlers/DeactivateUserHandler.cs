using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Enums;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class DeactivateUserHandler : IRequestHandler<DeactivateUserRequestModel, DeactivateUserResponseModel>
    {
        private readonly AppDbContext _context;
        public DeactivateUserHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<DeactivateUserResponseModel> Handle(DeactivateUserRequestModel request, CancellationToken cancellationToken)
        {
            var resp = new DeactivateUserResponseModel { UserId = request.UserId, HospitalId = request.HospitalId };

            if (request.CallerUserId == Guid.Empty)
            {
                resp.Success = false;
                resp.Message = "Could not resolve the signed-in user.";
                return resp;
            }

            // Deactivating yourself here would lock your own session out.
            if (request.UserId == request.CallerUserId)
            {
                resp.Success = false;
                resp.Message = "You can't deactivate your own account.";
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
                resp.Message = "Only an administrator can deactivate a member.";
                return resp;
            }

            // The target must be a member of the same hospital; protect the hospital owner.
            var targetMembership = await _context.HospitalUsers
                .Where(hu => hu.UserID == request.UserId && hu.HospitalID == request.HospitalId)
                .Select(hu => new { hu.IsPrimary })
                .FirstOrDefaultAsync(cancellationToken);
            if (targetMembership == null)
            {
                resp.Success = false;
                resp.Message = "This member is not part of your hospital.";
                return resp;
            }
            if (targetMembership.IsPrimary)
            {
                resp.Success = false;
                resp.Message = "The hospital owner's account can't be deactivated here.";
                return resp;
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == request.UserId && u.UserStatusId != (int)UserStatusEnum.Revoked, cancellationToken);
            var currentDateTime = DateTime.UtcNow;


            if (user == null)
            {
                resp.Success = false;
                resp.Message = "User not found";
                return resp;
            }

            try
            {
                user.UserStatusId = (int)UserStatusEnum.Revoked;

                var userAuth = await _context.UserAuths.FirstOrDefaultAsync(a => a.UserID == user.UserID, cancellationToken);
                if (userAuth != null)
                {
                    userAuth.IsLocked = true;
                    userAuth.UserStatusId = (int)UserStatusEnum.Revoked;
                }

                var userProfile = await _context.UserProfiles.FirstOrDefaultAsync(up => up.UserID == user.UserID, cancellationToken);
                if (userProfile != null)
                {
                    userProfile.UserStatusId = (int)UserStatusEnum.Revoked;
                }

                var userHistory = new UserHistory
                {
                    UserId = user.UserID,
                    UserStatusId = (int)UserStatusEnum.Revoked,
                    UpdatedBy = request.CallerUserId,
                    UpdatedDate = currentDateTime
                };
                _context.UserHistories.Add(userHistory);

                await _context.SaveChangesAsync(cancellationToken);

                resp.Success = true;
                resp.Message = "User access revoked";
                return resp;
            }
            catch (Exception)
            {
                resp.Success = false;
                resp.Message = "An error occurred while deactivating the user.";
                return resp;
            }
        }
    }
}
