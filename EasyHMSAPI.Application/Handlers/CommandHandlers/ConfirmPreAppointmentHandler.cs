using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Data.Enums;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>
    /// Front-desk "Confirm" action for a PRE_APPOINTMENT row. This is the genuine slot-commitment
    /// moment — nothing was reserved when the pre-appointment was booked publicly — so this is
    /// where the conflict check belongs (mirrors DoctorBookedSlotsHandler's own booked-slots query),
    /// and where status resolution + token allocation reuse the same shared helpers
    /// RegisterAppointmentHandler uses, so the rules never drift between the two paths. This is also
    /// the right point for the WhatsApp confirmation — a public pre-appointment has no real
    /// doctor/date/time/token yet to confirm, only this step does.
    /// </summary>
    public class ConfirmPreAppointmentHandler : IRequestHandler<ConfirmPreAppointmentRequestModel, ConfirmPreAppointmentResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IWhatsAppMessagingService _whatsAppMessagingService;
        private readonly IMemoryCache _cache;

        public ConfirmPreAppointmentHandler(AppDbContext context, IWhatsAppMessagingService whatsAppMessagingService, IMemoryCache cache)
        {
            _context = context;
            _whatsAppMessagingService = whatsAppMessagingService;
            _cache = cache;
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
            var previousApptDate = appointment.ApptDate;

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

            // Invalidate both the slot it moved OUT of and the one it moved INTO — the receptionist
            // may have picked a different date than the patient's original preference (that's the
            // whole point of the auto-search), so a stale cache could otherwise keep showing the
            // now-vacated original time as booked, or the newly-claimed time as open.
            _cache.Remove(PublicDirectoryCacheKeys.BookedSlots(request.HospitalId, appointment.DoctorId, previousApptDate));
            _cache.Remove(PublicDirectoryCacheKeys.BookedSlots(request.HospitalId, appointment.DoctorId, appointment.ApptDate));

            var tokenNumber = await AppointmentBookingHelpers.AllocateTokenWithLockingAsync(
                _context, request.HospitalId, appointment.DoctorId, appointment.ApptDate, appointment.ApptId, cancellationToken);

            var isReminderSent = false;
            try
            {
                var patient = await _context.PatientRegistrations
                    .FirstOrDefaultAsync(p => p.PatientId == appointment.PatientId, cancellationToken);

                if (patient != null && !string.IsNullOrWhiteSpace(patient.Mobile))
                {
                    var hospitalName = await _context.Hospitals
                        .Where(h => h.HospitalID == request.HospitalId)
                        .Select(h => h.Name)
                        .FirstOrDefaultAsync(cancellationToken);
                    var doctorName = await _context.Doctors
                        .Where(d => d.DoctorID == appointment.DoctorId)
                        .Select(d => d.User.UserProfiles.FirstOrDefault()!.FullName)
                        .FirstOrDefaultAsync(cancellationToken);

                    var token = string.Empty;
                    if (tokenNumber.HasValue && tokenNumber.Value > 0)
                    {
                        var groupIndex = (tokenNumber.Value - 1) / 30;
                        var prefix = (char)(65 + groupIndex);
                        var num = ((tokenNumber.Value - 1) % 30) + 1;
                        token = $"{prefix}-{num}";
                    }

                    isReminderSent = await _whatsAppMessagingService.SendAppointmentConfirmationAsync(
                        patient.Mobile,
                        patient.FullName ?? string.Empty,
                        hospitalName ?? string.Empty,
                        doctorName ?? string.Empty,
                        token,
                        appointment.ApptDate.ToString("dd-MM-yyyy"),
                        appointment.StartAt.ToString("HH:mm"));
                }
            }
            catch
            {
                // Best-effort — never fail the confirmation because WhatsApp delivery threw.
            }

            return new ConfirmPreAppointmentResponseModel
            {
                Success = true,
                Message = "Appointment confirmed.",
                AppointmentId = appointment.ApptId,
                Status = newStatus,
                TokenNumber = tokenNumber,
                IsReminderSent = isReminderSent,
            };
        }
    }
}
