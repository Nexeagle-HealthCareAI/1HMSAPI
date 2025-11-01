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
                // Validate appointment exists and belongs to the patient
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

                // Create or update vitals
                var vitals = await _context.AppointmentVitals
                    .FirstOrDefaultAsync(v => v.ApptId == request.AppointmentId && v.PatientId == request.PatientId, cancellationToken);

                if (vitals == null)
                {
                    vitals = new AppointmentVitals
                    {
                        VitalId = Guid.NewGuid(),
                        ApptId = request.AppointmentId,
                        HospitalId = appointment.HospitalId,
                        PatientId = appointment.PatientId,
                        VitalsJson = JsonSerializer.Serialize(request.VitalsJson),
                        BP_Sys = request?.VitalsJson?.Bp?.Sys,
                        BP_Dia = request?.VitalsJson?.Bp?.Dia,
                        Pulse = request?.VitalsJson?.Pulse,
                        TempC = request?.VitalsJson?.TempC,
                        SpO2 = (byte?)request?.VitalsJson?.Spo2,
                        HeightCm  = request?.VitalsJson?.HeightCm,
                        WeightKg = request?.VitalsJson?.WeightKg,
                        BMI = request?.VitalsJson?.Bmi,
                        RecordedAt = DateTime.UtcNow,
                        RecordedBy = request?.RecordedBy
                    };

                    appointment.CurrentStatusCode = AppConstants.AppointmentStatus_Ready;

                    _context.AppointmentVitals.Add(vitals);
                }
                else
                {
                    vitals.VitalsJson = JsonSerializer.Serialize(request.VitalsJson);
                    //vitals.BP_Sys = request?.VitalsJson?.Bp?.Sys;
                    //vitals.BP_Dia = request?.VitalsJson?.Bp?.Dia;
                    //vitals.Pulse = request?.VitalsJson?.Pulse;
                    //vitals.TempC = request?.VitalsJson?.TempC;
                    //vitals.SpO2 = (byte?)request?.VitalsJson?.Spo2;
                    //vitals.HeightCm = request?.VitalsJson?.HeightCm;
                    //vitals.WeightKg = request?.VitalsJson?.WeightKg;
                    //vitals.BMI = request?.VitalsJson?.Bmi;
                    vitals.RecordedAt = DateTime.UtcNow;
                    vitals.RecordedBy = request?.RecordedBy;

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
