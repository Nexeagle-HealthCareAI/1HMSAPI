using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Data.Enums;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>
    /// Front-desk "Confirm" action for a PRE_APPOINTMENT row. This is the genuine slot-commitment
    /// moment — nothing was reserved when the pre-appointment was booked publicly — so this is
    /// where the conflict check belongs (mirrors DoctorBookedSlotsHandler's own booked-slots query),
    /// and where status resolution + token allocation reuse the same shared helpers
    /// RegisterAppointmentHandler uses, so the rules never drift between the two paths.
    /// </summary>
    public class ConfirmPreAppointmentHandler : IRequestHandler<ConfirmPreAppointmentRequestModel, ConfirmPreAppointmentResponseModel>
    {
        private readonly AppDbContext _context;

        public ConfirmPreAppointmentHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ConfirmPreAppointmentResponseModel> Handle(ConfirmPreAppointmentRequestModel request, CancellationToken cancellationToken)
        {
            var appointment = await _context.Appointments
                .FirstOrDefaultAsync(a => a.ApptId == request.AppointmentId && a.HospitalId == request.HospitalId, cancellationToken);

            if (appointment == null)
                return new ConfirmPreAppointmentResponseModel { Success = false, Message = "Appointment not found." };

            if (appointment.CurrentStatusCode != AppConstants.AppointmentStatus_PreAppointment)
                return new ConfirmPreAppointmentResponseModel { Success = false, Message = "This appointment is not a pending pre-appointment." };

            var conflictExists = await (from a in _context.Appointments
                                         join d in _context.Doctors on a.DoctorId equals d.DoctorID
                                         join u in _context.Users on d.UserID equals u.UserID
                                         where a.DoctorId == appointment.DoctorId
                                               && a.HospitalId == request.HospitalId
                                               && a.ApptDate.Date == request.StartAt.Date
                                               && a.ApptId != appointment.ApptId
                                               && u.UserStatusId != (int)UserStatusEnum.Revoked
                                               && a.CurrentStatusCode != AppConstants.AppointmentStatus_Cancelled
                                               && a.StartAt.TimeOfDay == request.StartAt.TimeOfDay
                                         select a.ApptId).AnyAsync(cancellationToken);

            if (conflictExists)
                return new ConfirmPreAppointmentResponseModel { Success = false, Message = "The selected time slot is already booked." };

            var explicitDuration = request.SlotTimeInMinutes.HasValue && request.SlotTimeInMinutes.Value > 0 ? request.SlotTimeInMinutes.Value : 15;

            appointment.ApptDate = request.StartAt.Date;
            appointment.StartAt = request.StartAt;
            appointment.EndAt = request.StartAt.AddMinutes(explicitDuration);

            var newStatus = AppointmentBookingHelpers.ResolveInitialStatus(appointment.ApptDate);
            appointment.CurrentStatusCode = newStatus;
            appointment.LastStatusCodeAt = DateTime.UtcNow;

            var history = string.IsNullOrEmpty(appointment.StatusHistoryJson)
                ? new List<object>()
                : JsonSerializer.Deserialize<List<object>>(appointment.StatusHistoryJson) ?? new List<object>();
            history.Add(new { status = newStatus, timestamp = DateTime.UtcNow });
            appointment.StatusHistoryJson = JsonSerializer.Serialize(history);

            await _context.SaveChangesAsync(cancellationToken);

            var tokenNumber = await AppointmentBookingHelpers.AllocateTokenWithLockingAsync(
                _context, request.HospitalId, appointment.DoctorId, appointment.ApptDate, appointment.ApptId, cancellationToken);

            return new ConfirmPreAppointmentResponseModel
            {
                Success = true,
                Message = "Appointment confirmed.",
                AppointmentId = appointment.ApptId,
                Status = newStatus,
                TokenNumber = tokenNumber,
            };
        }
    }
}
