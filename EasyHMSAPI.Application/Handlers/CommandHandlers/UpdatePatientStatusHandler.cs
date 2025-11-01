using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class UpdatePatientStatusHandler : IRequestHandler<UpdatePatientStatusRequestModel, UpdatePatientStatusResponseModel>
    {
        private readonly AppDbContext _context;
        public UpdatePatientStatusHandler(AppDbContext context)
        {
            _context = context;
        }
        public async Task<UpdatePatientStatusResponseModel> Handle(UpdatePatientStatusRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                var appointment = await _context.Appointments
                    .FirstOrDefaultAsync(a => a.ApptId == request.AppointmentId && a.PatientId == request.PatientId, cancellationToken);

                if (appointment == null)
                {
                    return new UpdatePatientStatusResponseModel { Success = false, Message = "Appointment not found for the given patient." };
                }

                if (!string.Equals(appointment.CurrentStatusCode, request.CurrentStatus, StringComparison.OrdinalIgnoreCase))
                {
                    return new UpdatePatientStatusResponseModel { Success = false, Message = $"Current status does not match. Expected: {appointment.CurrentStatusCode}, Got: {request.CurrentStatus}" };
                }

                var statusHistory = string.IsNullOrEmpty(appointment.StatusHistoryJson)
                    ? new List<StatusHistoryItem>()
                    : JsonSerializer.Deserialize<List<StatusHistoryItem>>(appointment.StatusHistoryJson);
                statusHistory ??= new List<StatusHistoryItem>();
                statusHistory.Add(new StatusHistoryItem
                {
                    Status = request.ToStatus,
                    ChangedAt = DateTime.UtcNow,
                    ChangedBy = request.UserId.ToString()
                });
                appointment.CurrentStatusCode = request.ToStatus;
                appointment.LastStatusCodeAt = DateTime.UtcNow;
                appointment.StatusHistoryJson = JsonSerializer.Serialize(statusHistory);

                _context.Appointments.Update(appointment);
                await _context.SaveChangesAsync(cancellationToken);

                return new UpdatePatientStatusResponseModel
                {
                    Success = true,
                    Message = "Appointment status updated successfully.",
                    UpdatedAt = DateTime.UtcNow,
                    PreviousStatus = request.CurrentStatus,
                    NewStatus = request.ToStatus
                };
            }
            catch (Exception ex)
            {
                return new UpdatePatientStatusResponseModel { Success = false, Message = $"Error updating patient status: {ex.Message}" };
            }
        }
    }
}
