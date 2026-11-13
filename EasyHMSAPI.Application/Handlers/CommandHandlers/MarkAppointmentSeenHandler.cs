using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class MarkAppointmentSeenHandler : IRequestHandler<MarkAppointmentSeenRequestModel, MarkAppointmentSeenResponseModel>
    {
        private readonly AppDbContext _context;

        public MarkAppointmentSeenHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<MarkAppointmentSeenResponseModel> Handle(MarkAppointmentSeenRequestModel request, CancellationToken cancellationToken)
        {
            var appt = await _context.Appointments
                .FirstOrDefaultAsync(a => a.ApptId == request.AppointmentId && a.HospitalId == request.HospitalId, cancellationToken);
            if (appt == null)
                return new MarkAppointmentSeenResponseModel { Success = false, Message = "Appointment not found." };

            if (AppointmentSeen.Mark(appt, request.LoggedInUserId))
                await _context.SaveChangesAsync(cancellationToken);

            return new MarkAppointmentSeenResponseModel { Success = true, Message = "Marked as seen.", HospitalSeenAt = appt.HospitalSeenAt };
        }
    }
}
