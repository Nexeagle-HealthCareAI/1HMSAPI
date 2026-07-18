using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Data.Enums;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class DoctorBookedSlotsHandler : IRequestHandler<DoctorBookedSlotsRequestModel, DoctorBookedSlotsResponseModel>
    {
        private readonly AppDbContext _context;
        public DoctorBookedSlotsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<DoctorBookedSlotsResponseModel> Handle(DoctorBookedSlotsRequestModel request, CancellationToken cancellationToken)
        {
            var bookedSlots = await (from a in _context.Appointments
                                     join d in _context.Doctors on a.DoctorId equals d.DoctorID
                                     join u in _context.Users on d.UserID equals u.UserID
                                     where a.DoctorId == request.DoctorId && a.HospitalId == request.HospitalId && a.ApptDate.Date == request.Date.Date && u.UserStatusId != (int)UserStatusEnum.Revoked && a.CurrentStatusCode != AppConstants.AppointmentStatus_Cancelled
                                           && (request.ExcludeAppointmentId == null || a.ApptId != request.ExcludeAppointmentId.Value)
                                     select a.StartAt.TimeOfDay)
                                     .ToListAsync(cancellationToken);

            return new DoctorBookedSlotsResponseModel
            {
                DoctorId = request.DoctorId,
                Date = request.Date.Date,
                BookedSlots = bookedSlots
            };
        }
    }
}
