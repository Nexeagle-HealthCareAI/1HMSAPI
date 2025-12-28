using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Data.Enums;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class CancelAppointmentHandler : IRequestHandler<CancelAppointmentRequestModel, CancelAppointmentResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly ISmsService _smsService;

        public CancelAppointmentHandler(AppDbContext context, ISmsService smsService)
        {
            _context = context;
            _smsService = smsService;
        }

        public async Task<CancelAppointmentResponseModel> Handle(CancelAppointmentRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                var appt = await _context.Appointments
                    .FirstOrDefaultAsync(a => a.ApptId == request.AppointmentId && a.PatientId == request.PatientId, cancellationToken);

                if (appt == null)
                    return new CancelAppointmentResponseModel { Success = false, Message = "Appointment not found." };

                // Check doctor status before proceeding
                if (appt != null)
                {
                    var doctorActive = await _context.Doctors.AnyAsync(d => d.DoctorID == appt.DoctorId && d.User.UserStatusId != (int)UserStatusEnum.Revoked, cancellationToken);
                    if (!doctorActive)
                    {
                        return new CancelAppointmentResponseModel { Success = false, Message = "Doctor is not active or has been revoked." };
                    }

                    appt.CurrentStatusCode = AppConstants.AppointmentStatus_Cancelled;
                    appt.LastStatusCodeAt = DateTime.UtcNow;
                    var history = string.IsNullOrEmpty(appt?.StatusHistoryJson)
                   ? new List<object>()
                   : JsonSerializer.Deserialize<List<object>>(appt.StatusHistoryJson) ?? new List<object>();
                    history.Add(new { status = AppConstants.AppointmentStatus_Cancelled, timestamp = DateTime.UtcNow.ToString("o") });
                    if(appt?.StatusHistoryJson != null)
                    {
                        appt.StatusHistoryJson = JsonSerializer.Serialize(history);
                        var token = await _context.AppointmentTokens
                       .Where(t => t.ApptId == appt.ApptId)
                       .FirstOrDefaultAsync(cancellationToken);

                        if (token != null)
                        {
                            token.TokenNo = 0;
                            _context.AppointmentTokens.Update(token);
                        }

                        _context.Appointments.Update(appt);
                    }
                }
                await _context.SaveChangesAsync(cancellationToken);

                // Send SMS to patient
                var patientId = appt?.PatientId;
                var patient = await _context.PatientRegistrations.FirstOrDefaultAsync(p => p.PatientId == patientId, cancellationToken);
                bool isSmsSent = false;
                if (patient != null && !string.IsNullOrWhiteSpace(patient.Mobile))
                {
                    var smsMsg = $"Dear {patient.FullName}, your appointment on {appt?.ApptDate:yyyy-MM-dd} at {appt?.StartAt:HH:mm} has been cancelled.";
                    isSmsSent = await _smsService.SendInvitationSmsAsync(patient.Mobile, smsMsg);
                }

                return new CancelAppointmentResponseModel {
                    Success = true,
                    FinalStatus = AppConstants.AppointmentStatus_Cancelled,
                    IsReminderSent = isSmsSent,
                    Message = "Appointment cancelled successfully."
                };
            }
            catch (Exception ex)
            {
                return new CancelAppointmentResponseModel { Success = false, Message = ex.Message };
            }
        }
    }
}