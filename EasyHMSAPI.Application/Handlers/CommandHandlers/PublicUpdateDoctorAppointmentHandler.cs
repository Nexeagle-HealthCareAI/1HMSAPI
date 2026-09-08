using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    // Anonymous/bot-facing doctor reassignment on an existing appointment — the "update" action of
    // the WhatsApp "check my appointment" flow (cancel/update/book-another). Same
    // AppointmentId-as-secret + Mobile-as-second-factor gate as PublicCancelAppointmentHandler.
    //
    // Deliberately stricter than PublicRescheduleAppointmentHandler's optional ToDoctorId path in
    // two ways, since reassigning the doctor is this endpoint's entire purpose rather than an
    // incidental side field:
    //   1. The new doctor must resolve via PublicDirectoryHelpers.ResolvePubliclyListedHospitalIdAsync
    //      (publicly listed, not revoked) AND resolve to this appointment's own HospitalId —
    //      reschedule's ToDoctorId only checks "not revoked", which would let a caller move an
    //      appointment to any active doctor in the database, at any hospital.
    //   2. Blocked once the visit is under way (READY/UNDER_CONSULT/VITALS_REQUIRED/
    //      LAB_REQUIRED/AWAITING_RECONSULT), not just CANCELLED/COMPLETED — reassigning the doctor
    //      mid-consult isn't a sensible operation, unlike a plain reschedule.
    public class PublicUpdateDoctorAppointmentHandler : IRequestHandler<PublicUpdateDoctorAppointmentRequestModel, PublicUpdateDoctorAppointmentResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly ISmsService _smsService;
        private readonly ILogger<PublicUpdateDoctorAppointmentHandler> _logger;
        private readonly IMemoryCache _cache;

        public PublicUpdateDoctorAppointmentHandler(AppDbContext context, ISmsService smsService, ILogger<PublicUpdateDoctorAppointmentHandler> logger, IMemoryCache cache)
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

        private static readonly string[] InProgressStatuses =
        {
            AppConstants.AppointmentStatus_Ready,
            AppConstants.AppointmentStatus_UnderConsult,
            AppConstants.AppointmentStatus_VitalsRequired,
            AppConstants.AppointmentStatus_LabRequired,
            AppConstants.AppointmentStatus_AwaitingReconsult,
        };

        public async Task<PublicUpdateDoctorAppointmentResponseModel> Handle(PublicUpdateDoctorAppointmentRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                var appt = await _context.Appointments
                    .FirstOrDefaultAsync(a => a.ApptId == request.AppointmentId, cancellationToken);

                if (appt == null)
                    return new PublicUpdateDoctorAppointmentResponseModel { Success = false, Message = "Appointment not found." };

                var patient = await _context.PatientRegistrations
                    .FirstOrDefaultAsync(p => p.PatientId == appt.PatientId, cancellationToken);

                // Same generic message on a mobile mismatch as "not found" — don't tell an
                // unauthenticated caller which half of the lookup failed.
                if (patient == null || NormalizeMobile(patient.Mobile) != NormalizeMobile(request.Mobile) || NormalizeMobile(request.Mobile) == string.Empty)
                    return new PublicUpdateDoctorAppointmentResponseModel { Success = false, Message = "Appointment not found." };

                if (appt.CurrentStatusCode == AppConstants.AppointmentStatus_Cancelled)
                    return new PublicUpdateDoctorAppointmentResponseModel { Success = false, Message = "This appointment was cancelled — book a new appointment instead." };

                if (appt.CurrentStatusCode == AppConstants.AppointmentStatus_Completed)
                    return new PublicUpdateDoctorAppointmentResponseModel { Success = false, Message = "Cannot change the doctor on a completed appointment." };

                if (InProgressStatuses.Contains(appt.CurrentStatusCode))
                    return new PublicUpdateDoctorAppointmentResponseModel { Success = false, Message = "This appointment is already in progress — its doctor can no longer be changed." };

                if (request.NewDoctorId == Guid.Empty)
                    return new PublicUpdateDoctorAppointmentResponseModel { Success = false, Message = "NewDoctorId is required." };

                if (request.NewDoctorId == appt.DoctorId)
                    return new PublicUpdateDoctorAppointmentResponseModel { Success = false, Message = "You're already booked with this doctor." };

                var newDoctorHospitalId = await PublicDirectoryHelpers.ResolvePubliclyListedHospitalIdAsync(_context, request.NewDoctorId, cancellationToken);
                if (newDoctorHospitalId == null)
                    return new PublicUpdateDoctorAppointmentResponseModel { Success = false, Message = "Doctor not found." };

                if (newDoctorHospitalId.Value != appt.HospitalId)
                    return new PublicUpdateDoctorAppointmentResponseModel { Success = false, Message = "Doctor not found for this hospital." };

                var originalDoctorId = appt.DoctorId;
                appt.DoctorId = request.NewDoctorId;
                appt.LastStatusCodeAt = DateTime.UtcNow;

                var history = string.IsNullOrEmpty(appt.StatusHistoryJson)
                    ? new List<object>()
                    : System.Text.Json.JsonSerializer.Deserialize<List<object>>(appt.StatusHistoryJson) ?? new List<object>();
                history.Add(new { status = appt.CurrentStatusCode, timestamp = DateTime.UtcNow.ToString("o"), reason = $"Doctor changed from {originalDoctorId} to {request.NewDoctorId}" });
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

                // Free the vacated original doctor's slot cache too, not just the new one — same
                // reasoning as PublicRescheduleAppointmentHandler's cache handling.
                _cache.Remove(PublicDirectoryCacheKeys.BookedSlots(appt.HospitalId, originalDoctorId, appt.ApptDate));
                _cache.Remove(PublicDirectoryCacheKeys.BookedSlots(appt.HospitalId, appt.DoctorId, appt.ApptDate));

                var hospitalName = await _context.Hospitals
                    .Where(h => h.HospitalID == appt.HospitalId)
                    .Select(h => h.Name)
                    .FirstOrDefaultAsync(cancellationToken);
                var doctorName = await _context.Doctors
                    .Where(d => d.DoctorID == appt.DoctorId)
                    .Select(d => d.User.UserProfiles.FirstOrDefault()!.FullName)
                    .FirstOrDefaultAsync(cancellationToken);
                doctorName ??= "Doctor";

                var token = tokenNo.HasValue ? new TokenInfo { TokenNo = tokenNo.Value, TokenDate = appt.ApptDate.Date } : null;

                if (!string.IsNullOrWhiteSpace(patient.Mobile))
                {
                    var smsMsg = $"Dear {patient.FullName}, your appointment on {appt.ApptDate:yyyy-MM-dd} has been moved to Dr. {doctorName}.";
                    var isSmsSent = await _smsService.SendInvitationSmsAsync(patient.Mobile, smsMsg);
                    _logger.LogInformation("Public-updated doctor on appointment {AppointmentId}, SMS sent: {IsSmsSent}", appt.ApptId, isSmsSent);
                }

                return new PublicUpdateDoctorAppointmentResponseModel
                {
                    Success = true,
                    Message = $"Your appointment has been moved to Dr. {doctorName}.",
                    Appointment = new PublicAppointmentSummary
                    {
                        AppointmentId = appt.ApptId,
                        DoctorName = doctorName,
                        HospitalName = hospitalName ?? "Hospital",
                        ApptDate = appt.ApptDate,
                        StartAt = appt.StartAt,
                        Status = PublicAppointmentStatusLabels.ToPatientLabel(appt.CurrentStatusCode),
                        StatusCode = appt.CurrentStatusCode ?? string.Empty,
                    },
                    Token = token,
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating doctor on appointment {AppointmentId} via public endpoint", request.AppointmentId);
                return new PublicUpdateDoctorAppointmentResponseModel { Success = false, Message = "An error occurred while updating the doctor." };
            }
        }
    }
}
