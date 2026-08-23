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
using Microsoft.Extensions.Logging;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    // Anonymous/bot-facing equivalent of RescheduleAppointmentHandler — fully independent
    // implementation, same reasoning as PublicCancelAppointmentHandler. Two deliberate
    // differences from the staff handler (not backported there, per the "fully separate" scope):
    //   1. Also invalidates the booked-slots cache for the ORIGINAL date/doctor, not just
    //      relying on the new slot's cache entry — the staff handler never did this, so a stale
    //      "still booked" cache entry could linger for the vacated original slot until its TTL.
    //   2. No ExpectVersion field — the staff handler's version only ever accepts literally 0
    //      (no real optimistic-concurrency column exists on Appointment), so it wasn't carried
    //      into this new request shape at all.
    public class PublicRescheduleAppointmentHandler : IRequestHandler<PublicRescheduleAppointmentRequestModel, PublicRescheduleAppointmentResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly ISmsService _smsService;
        private readonly ILogger<PublicRescheduleAppointmentHandler> _logger;
        private readonly IMemoryCache _cache;

        public PublicRescheduleAppointmentHandler(AppDbContext context, ISmsService smsService, ILogger<PublicRescheduleAppointmentHandler> logger, IMemoryCache cache)
        {
            _context = context;
            _smsService = smsService;
            _logger = logger;
            _cache = cache;
        }

        private static string NormalizeMobile(string? mobile)
        {
            if (string.IsNullOrWhiteSpace(mobile)) return string.Empty;
            var digits = new string(mobile.Where(char.IsDigit).ToArray());
            if (digits.Length == 12 && digits.StartsWith("91")) digits = digits[2..];
            else if (digits.Length == 11 && digits.StartsWith("0")) digits = digits[1..];
            return digits;
        }

        public async Task<PublicRescheduleAppointmentResponseModel> Handle(PublicRescheduleAppointmentRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                var appt = await _context.Appointments
                    .FirstOrDefaultAsync(a => a.ApptId == request.AppointmentId, cancellationToken);

                if (appt == null)
                    return new PublicRescheduleAppointmentResponseModel { Success = false, Message = "Appointment not found." };

                var patient = await _context.PatientRegistrations
                    .FirstOrDefaultAsync(p => p.PatientId == appt.PatientId, cancellationToken);

                if (patient == null || NormalizeMobile(patient.Mobile) != NormalizeMobile(request.Mobile) || NormalizeMobile(request.Mobile) == string.Empty)
                    return new PublicRescheduleAppointmentResponseModel { Success = false, Message = "Appointment not found." };

                if (appt.CurrentStatusCode == AppConstants.AppointmentStatus_Cancelled)
                    return new PublicRescheduleAppointmentResponseModel { Success = false, Message = "This appointment was cancelled — book a new appointment instead." };

                if (appt.CurrentStatusCode == AppConstants.AppointmentStatus_Completed)
                    return new PublicRescheduleAppointmentResponseModel { Success = false, Message = "Cannot reschedule a completed appointment." };

                if (request.ToApptDate.Date <= DateTime.UtcNow.Date)
                    return new PublicRescheduleAppointmentResponseModel { Success = false, Message = "Reschedule date must be in the future." };

                var originalDate = appt.ApptDate;
                var originalDoctorId = appt.DoctorId;

                appt.ApptDate = request.ToApptDate.Date;
                if (request.ToStartAt.HasValue)
                {
                    appt.StartAt = request.ToStartAt.Value;
                }
                if (appt.EndAt <= appt.StartAt)
                {
                    appt.EndAt = appt.StartAt.AddMinutes(15);
                }
                else
                {
                    var duration = appt.EndAt - appt.StartAt;
                    appt.EndAt = appt.StartAt.Add(duration);
                }

                if (request.ToDoctorId.HasValue)
                {
                    var doctorActive = await _context.Doctors.AnyAsync(d => d.DoctorID == request.ToDoctorId.Value && d.User.UserStatusId != (int)UserStatusEnum.Revoked, cancellationToken);
                    if (!doctorActive)
                    {
                        return new PublicRescheduleAppointmentResponseModel { Success = false, Message = "Doctor is not active or has been revoked." };
                    }

                    appt.DoctorId = request.ToDoctorId.Value;
                }

                appt.CurrentStatusCode = AppConstants.AppointmentStatus_Future;
                appt.LastStatusCodeAt = DateTime.UtcNow;

                var history = string.IsNullOrEmpty(appt.StatusHistoryJson)
                    ? new List<object>()
                    : System.Text.Json.JsonSerializer.Deserialize<List<object>>(appt.StatusHistoryJson) ?? new List<object>();
                history.Add(new { status = AppConstants.AppointmentStatus_Future, timestamp = DateTime.UtcNow.ToString("o"), reason = request.Reason });
                appt.StatusHistoryJson = System.Text.Json.JsonSerializer.Serialize(history);

                _context.Appointments.Update(appt);

                var hadToken = await _context.AppointmentTokens.AnyAsync(t => t.ApptId == appt.ApptId, cancellationToken);
                int? tokenNo = null;
                if (hadToken)
                {
                    tokenNo = await AppointmentBookingHelpers.AllocateTokenWithLockingAsync(
                        _context, appt.HospitalId, appt.DoctorId, appt.ApptDate, appt.ApptId, cancellationToken);
                }

                await _context.SaveChangesAsync(cancellationToken);

                // Free the vacated original slot's cache entry too, not just the new one implied
                // by the fresh DoctorBookedSlotsHandler query — see file-header note.
                _cache.Remove(PublicDirectoryCacheKeys.BookedSlots(appt.HospitalId, originalDoctorId, originalDate));
                _cache.Remove(PublicDirectoryCacheKeys.BookedSlots(appt.HospitalId, appt.DoctorId, appt.ApptDate));

                var token = tokenNo.HasValue ? new TokenInfo { TokenNo = tokenNo.Value, TokenDate = appt.ApptDate.Date } : null;

                if (!string.IsNullOrWhiteSpace(patient.Mobile))
                {
                    var smsMsg = $"Dear {patient.FullName}, your appointment has been rescheduled to {appt.ApptDate:yyyy-MM-dd} at {appt.StartAt:HH:mm}.";
                    var isSmsSent = await _smsService.SendInvitationSmsAsync(patient.Mobile, smsMsg);
                    _logger.LogInformation("Public-rescheduled appointment {AppointmentId}, SMS sent: {IsSmsSent}", appt.ApptId, isSmsSent);
                }

                return new PublicRescheduleAppointmentResponseModel
                {
                    ApptId = appt.ApptId,
                    FinalStatus = AppConstants.AppointmentStatus_Future,
                    Token = token,
                    Success = true,
                    Message = "Appointment rescheduled successfully."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rescheduling appointment {AppointmentId} via public endpoint", request.AppointmentId);
                return new PublicRescheduleAppointmentResponseModel { Success = false, Message = "An error occurred while rescheduling the appointment." };
            }
        }
    }
}
