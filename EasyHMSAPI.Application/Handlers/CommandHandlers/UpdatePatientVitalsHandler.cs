using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class UpdatePatientVitalsHandler : IRequestHandler<UpdatePatientVitalsRequestModel, UpdatePatientVitalsResponseModel>
    {
        private readonly AppDbContext _context;

        public UpdatePatientVitalsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<UpdatePatientVitalsResponseModel> Handle(UpdatePatientVitalsRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                var appointment = await _context.Appointments
                    .FirstOrDefaultAsync(a => a.ApptId == request.AppointmentId && a.PatientId == request.PatientId, cancellationToken);

                if (appointment == null)
                {
                    return new UpdatePatientVitalsResponseModel
                    {
                        Success = false,
                        Message = "Appointment not found for the given patient."
                    };
                }

                var vitals = await _context.AppointmentVitals
                    .FirstOrDefaultAsync(v => v.ApptId == request.AppointmentId && v.PatientId == request.PatientId, cancellationToken);

                if (vitals == null)
                {
                    vitals = new AppointmentVitals
                    {
                        VitalId = Guid.NewGuid(),
                        ApptId = request.AppointmentId,
                        HospitalId = appointment.HospitalId,
                        PatientId = appointment.PatientId ?? string.Empty,
                        VitalsJson = JsonSerializer.Serialize(request.VitalsJson),
                        RecordedAt = DateTime.UtcNow,
                        RecordedBy = request.RecordedBy
                    };

                    appointment.CurrentStatusCode = AppConstants.AppointmentStatus_Ready;
                    var history = string.IsNullOrEmpty(appointment.StatusHistoryJson)
                                    ? new List<object>()
                                    : JsonSerializer.Deserialize<List<object>>(appointment.StatusHistoryJson) ?? new List<object>();
                    history.Add(new { status = AppConstants.AppointmentStatus_Ready, timestamp = DateTime.Now });
                    appointment.StatusHistoryJson = JsonSerializer.Serialize(history);
                    appointment.LastStatusCodeAt = DateTime.UtcNow;

                    _context.AppointmentVitals.Add(vitals);
                }
                else
                {
                    vitals.VitalsJson = JsonSerializer.Serialize(request.VitalsJson);
                    vitals.RecordedAt = DateTime.UtcNow;
                    vitals.RecordedBy = request.RecordedBy;

                    appointment.CurrentStatusCode = AppConstants.AppointmentStatus_Ready;
                    var history = string.IsNullOrEmpty(appointment.StatusHistoryJson)
                                   ? new List<object>()
                                   : JsonSerializer.Deserialize<List<object>>(appointment.StatusHistoryJson) ?? new List<object>();
                    history.Add(new { status = AppConstants.AppointmentStatus_Ready, timestamp = DateTime.Now });
                    appointment.StatusHistoryJson = JsonSerializer.Serialize(history);
                    appointment.LastStatusCodeAt = DateTime.UtcNow;

                    _context.AppointmentVitals.Update(vitals);
                }

                await _context.SaveChangesAsync(cancellationToken);

                return new UpdatePatientVitalsResponseModel
                {
                    Success = true,
                    Message = "Patient vitals recorded successfully.",
                    VitalId = vitals.VitalId,
                    RecordedAt = vitals.RecordedAt,
                    RecordedBy = vitals.RecordedBy
                };
            }
            catch (Exception ex)
            {
                return new UpdatePatientVitalsResponseModel
                {
                    Success = false,
                    Message = $"Error recording patient vitals: {ex.Message}"
                };
            }
        }
    }
}
