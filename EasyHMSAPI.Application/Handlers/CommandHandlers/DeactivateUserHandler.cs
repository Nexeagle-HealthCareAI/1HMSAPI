using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
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

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserID == request.UserId, cancellationToken);
            if (user == null)
            {
                resp.Success = false;
                resp.Message = "User not found";
                return resp;
            }

            try
            {
                user.IsActive = false;
                _context.Users.Update(user);

                var auth = await _context.UserAuths.FirstOrDefaultAsync(a => a.UserID == request.UserId, cancellationToken);
                if (auth != null)
                {
                    auth.IsLocked = true;
                    _context.UserAuths.Update(auth);
                }

                await _context.SaveChangesAsync(cancellationToken);

                resp.Success = true;
                resp.Message = "User deactivated and access removed.";
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
