using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class GeneratePrescriptionHandler : IRequestHandler<GeneratePrescriptionRequestModel, GeneratePrescriptionResponseModel>
    {
        private readonly AppDbContext _context;

        public GeneratePrescriptionHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GeneratePrescriptionResponseModel> Handle(GeneratePrescriptionRequestModel request, CancellationToken cancellationToken)
        {
            var response = new GeneratePrescriptionResponseModel
            {
                Success = false,
                AppointmentId = request.AppointmentId,
                Data = new GeneratePrescriptionDataModel()
            };

            try
            {
                var prescriptionSettings = await _context.PrescriptionSettings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(ps => ps.DoctorId == request.DoctorId && ps.HospitalId == request.HospitalId, cancellationToken);

                var templateModel = new PrescriptionTemplateModel();
                if (prescriptionSettings != null)
                {
                    templateModel = new PrescriptionTemplateModel
                    {
                        PrescriptionSettingsId = prescriptionSettings.PrescriptionSettingId,
                        HospitalId = prescriptionSettings.HospitalId,
                        DoctorId = prescriptionSettings.DoctorId,
                        HeaderHeight = prescriptionSettings.HeaderHeight ?? 0,
                        FooterHeight = prescriptionSettings.FooterHeight ?? 0,
                        ContentLeftMargin = prescriptionSettings.ContentLeftMargin ?? 0,
                        ContentRightMargin = prescriptionSettings.ContentRightMargin ?? 0,
                        OverFlowPage = prescriptionSettings.OverFlowPage ?? false,
                        FontFamily = prescriptionSettings.FontFamily,
                        FontSize = prescriptionSettings.FontSize ?? 0,
                        FontWeight = prescriptionSettings.FontWeight,
                        TextColour = prescriptionSettings.TextColour,
                        Uri = prescriptionSettings.URI,
                        CreatedBy = null,
                        CreatedAtUtc = prescriptionSettings.CreatedAt,
                        UpdatedAtUtc = prescriptionSettings.UpdatedAt
                    };
                }

                var patientRegistration = await _context.PatientRegistrations
                    .AsNoTracking()
                    .FirstOrDefaultAsync(pr => pr.PatientId == request.PatientId && pr.HospitalId == request.HospitalId, cancellationToken);

                var patientDetails = new List<PatientDetailsModel>();
                if (patientRegistration != null)
                {
                    patientDetails.Add(new PatientDetailsModel
                    {
                        PatientId = patientRegistration.PatientId,
                        Name = patientRegistration.FullName,
                        Age = patientRegistration.AgeYears ?? 0,
                        Sex = patientRegistration.Sex,
                        Address = patientRegistration.AddressLine,
                        Contact = patientRegistration.Mobile
                    });
                }

                var appointmentVitals = await _context.AppointmentVitals
                    .AsNoTracking()
                    .FirstOrDefaultAsync(v => v.ApptId == request.AppointmentId && v.PatientId == request.PatientId, cancellationToken);

                var vitalsModel = new PatientVitalsModel
                {
                    Bp = new BloodPressureModel { Sys = 0, Dia = 0 },
                    Pulse = 0,
                    TempC = 0,
                    Spo2 = 0,
                    HeightCm = 0,
                    WeightKg = 0,
                    Bmi = 0
                };

                if (appointmentVitals != null && !string.IsNullOrWhiteSpace(appointmentVitals.VitalsJson))
                {
                    try
                    {
                        var vitalsJson = JsonSerializer.Deserialize<JsonElement>(appointmentVitals.VitalsJson);
                        vitalsModel = ParseVitalsFromJson(vitalsJson);
                    }
                    catch
                    {

                    }
                }

                response.Data = new GeneratePrescriptionDataModel
                {
                    Template = templateModel,
                    PatientData = new PatientPrescriptionDataModel
                    {
                        PatientDetails = patientDetails,
                        Vitals = vitalsModel
                    }
                };

                response.Success = true;
            }
            catch
            {
                response.Success = false;
                response.Data = new GeneratePrescriptionDataModel();
            }

            return response;
        }

        private PatientVitalsModel ParseVitalsFromJson(JsonElement vitalsJson)
        {
            var model = new PatientVitalsModel
            {
                Bp = new BloodPressureModel { Sys = 0, Dia = 0 },
                Pulse = 0,
                TempC = 0,
                Spo2 = 0,
                HeightCm = 0,
                WeightKg = 0,
                Bmi = 0
            };

            try
            {
                if (vitalsJson.TryGetProperty("Bp", out var bpElement) && bpElement.ValueKind != JsonValueKind.Null)
                {
                    model.Bp = new BloodPressureModel
                    {
                        Sys = bpElement.TryGetProperty("Sys", out var sys) ? sys.GetInt32() : 0,
                        Dia = bpElement.TryGetProperty("Dia", out var dia) ? dia.GetInt32() : 0
                    };
                }

                if (vitalsJson.TryGetProperty("Pulse", out var pulse))
                    model.Pulse = pulse.GetInt32();

                if (vitalsJson.TryGetProperty("TempC", out var tempC))
                    model.TempC = tempC.GetInt32();

                if (vitalsJson.TryGetProperty("Spo2", out var spo2))
                    model.Spo2 = spo2.GetInt32();

                if (vitalsJson.TryGetProperty("HeightCm", out var heightCm))
                    model.HeightCm = heightCm.GetInt32();

                if (vitalsJson.TryGetProperty("WeightKg", out var weightKg))
                    model.WeightKg = weightKg.GetInt32();

                if (vitalsJson.TryGetProperty("Bmi", out var bmi))
                    model.Bmi = bmi.GetDouble();
            }
            catch
            {
                // Return default values
            }

            return model;
        }
    }
}
