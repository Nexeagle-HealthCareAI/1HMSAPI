using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Data.Enums;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class RescheduleAppointmentHandler : IRequestHandler<RescheduleAppointmentRequestModel, RescheduleAppointmentResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly ISmsService _smsService;

        public RescheduleAppointmentHandler(AppDbContext context, ISmsService smsService)
        {
            _context = context;
            _smsService = smsService;
        }

        public async Task<RescheduleAppointmentResponseModel> Handle(RescheduleAppointmentRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                var appt = await _context.Appointments
                    .FirstOrDefaultAsync(a => a.ApptId == request.AppointmentId && a.PatientId == request.PatientId, cancellationToken);

                if (appt == null)
                    return new RescheduleAppointmentResponseModel { Success = false, Message = "Appointment not found." };

                if (request.ExpectVersion != 0)
                {
                    return new RescheduleAppointmentResponseModel { Success = false, Message = "Appointment version mismatch." };
                }

                if (request.ToApptDate.Date <= DateTime.UtcNow.Date)
                {
                    return new RescheduleAppointmentResponseModel { Success = false, Message = "Reschedule date must be in the future." };
                }

                // update appointment date/time/doctor
                appt.ApptDate = request.ToApptDate.Date;
                if (request.ToStartAt.HasValue)
                {
                    appt.StartAt = request.ToStartAt.Value;
                }
                // Ensure EndAt > StartAt, default to 15 minutes if invalid
                if (appt.EndAt <= appt.StartAt)
                {
                    appt.EndAt = appt.StartAt.AddMinutes(15);
                }
                else
                {
                    var duration = appt.EndAt - appt.StartAt;
                    appt.EndAt = appt.StartAt.Add(duration);
                }

                // Check doctor status before proceeding
                if (request.ToDoctorId.HasValue)
                {
                    var doctorActive = await _context.Doctors.AnyAsync(d => d.DoctorID == request.ToDoctorId.Value && d.User.UserStatusId != (int)UserStatusEnum.Revoked, cancellationToken);
                    if (!doctorActive)
                    {
                        return new RescheduleAppointmentResponseModel { Success = false, Message = "Doctor is not active or has been revoked." };
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

                // Get and update token for this appointment
                var token = await _context.AppointmentTokens
                    .FirstOrDefaultAsync(t => t.ApptId == appt.ApptId, cancellationToken);
                if (token != null)
                {
                    token.DoctorId = appt.DoctorId;
                    token.HospitalId = appt.HospitalId;
                    token.TokenDate = appt.ApptDate.Date;
                    token.CreatedAt = DateTime.UtcNow;

                    var otherTokensCount = await _context.AppointmentTokens
                        .CountAsync(t => t.DoctorId == appt.DoctorId && t.TokenDate == appt.ApptDate.Date && t.ApptId != appt.ApptId, cancellationToken);
                    token.TokenNo = otherTokensCount == 0 ? 1 : otherTokensCount + 1;

                    _context.AppointmentTokens.Update(token);
                }

                await _context.SaveChangesAsync(cancellationToken);

                // Send SMS to patient
                var patient = await _context.PatientRegistrations.FirstOrDefaultAsync(p => p.PatientId == appt.PatientId, cancellationToken);
                bool isSmsSent = false;
                if (patient != null && !string.IsNullOrWhiteSpace(patient.Mobile))
                {
                    var smsMsg = $"Dear {patient.FullName}, your appointment has been rescheduled to {appt.ApptDate:yyyy-MM-dd} at {appt.StartAt:HH:mm}.";
                    isSmsSent = await _smsService.SendInvitationSmsAsync(patient.Mobile, smsMsg);
                }

                return new RescheduleAppointmentResponseModel
                {
                    ApptId = appt.ApptId,
                    FinalStatus = AppConstants.AppointmentStatus_Future,
                    Token = token != null ? new TokenInfo { TokenNo = token.TokenNo, TokenDate = token.TokenDate } : null,
                    Success = true,
                    IsReminderSent = isSmsSent,
                    Message = "Appointment rescheduled successfully."
                };
            }
            catch (Exception ex)
            {
                return new RescheduleAppointmentResponseModel { Success = false, Message = ex.Message };
            }
        }
    }
}
