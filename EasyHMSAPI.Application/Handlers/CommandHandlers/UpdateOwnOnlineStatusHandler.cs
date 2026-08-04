using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class UpdateOwnOnlineStatusHandler : IRequestHandler<UpdateOwnOnlineStatusRequestModel, UpdateDoctorOnlineStatusResponseModel>
    {
        private readonly AppDbContext _context;

        public UpdateOwnOnlineStatusHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<UpdateDoctorOnlineStatusResponseModel> Handle(UpdateOwnOnlineStatusRequestModel request, CancellationToken cancellationToken)
        {
            var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.UserID == request.CallerUserId, cancellationToken);
            if (doctor == null)
                return new UpdateDoctorOnlineStatusResponseModel { Success = false, Message = "No doctor profile found for the signed-in user." };

            doctor.IsOnlineNow = request.IsOnlineNow;
            await _context.SaveChangesAsync(cancellationToken);

            return new UpdateDoctorOnlineStatusResponseModel { Success = true, Message = "Online status saved." };
        }
    }
}
