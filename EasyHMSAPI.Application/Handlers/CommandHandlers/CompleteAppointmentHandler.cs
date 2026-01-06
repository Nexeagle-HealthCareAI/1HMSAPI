using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class CompleteAppointmentHandler : IRequestHandler<CompleteAppointmentRequestModel, CompleteAppointmentResponseModel>
    {
        private readonly AppDbContext _context;

        public CompleteAppointmentHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<CompleteAppointmentResponseModel> Handle(CompleteAppointmentRequestModel request, CancellationToken cancellationToken)
        {
            CompleteAppointmentResponseModel response = new();
            try
            {
                var patientIdToLower = request.PatientId?.ToLower();
                var existingAppointment = await _context.Appointments
                    .Where(x => x.ApptId == request.AppointmentId
                            && x.DoctorId == request.DoctordId
                            && x.HospitalId == request.HospitalId
                            && !string.IsNullOrEmpty(x.PatientId)
                            && x.PatientId.ToLower() == patientIdToLower)
                    .FirstOrDefaultAsync(cancellationToken);
                if (existingAppointment is not null)
                {
                    existingAppointment.CurrentStatusCode = AppConstants.AppointmentStatus_Completed;
                    existingAppointment.LastStatusCodeAt = DateTime.UtcNow;
                    var history = string.IsNullOrEmpty(existingAppointment.StatusHistoryJson)
                        ? new List<object>()
                        : JsonSerializer.Deserialize<List<object>>(existingAppointment.StatusHistoryJson) ?? new List<object>();
                    history.Add(new { status = AppConstants.AppointmentStatus_Completed, timestamp =DateTime.UtcNow });
                    existingAppointment.StatusHistoryJson = JsonSerializer.Serialize(history);

                    await _context.SaveChangesAsync(cancellationToken);
                }
                else
                {
                    response.Success = false;
                    response.Message = "Appointment not found.";
                }
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = ex.Message + ex.InnerException + ex.StackTrace;
            }

            return response;
        }
    }
}
