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
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == request.UserId && u.UserStatusId != (int)UserStatusEnum.Revoked, cancellationToken);
            var currentDateTime = DateTime.Now;


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
                    UpdatedBy = request.PerformedByUserId,
                    UpdatedDate = currentDateTime
                };
                _context.UserHistories.Add(userHistory);

                await _context.SaveChangesAsync(cancellationToken);

                resp.Success = true;
                resp.Message = "User access revoked";
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
