using EasyHMSAPI.Application.Helpers.Interfaces;
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
        private readonly IDoctorValidationHelper _doctorValidationHelper;

        public GeneratePrescriptionHandler(AppDbContext context, IDoctorValidationHelper doctorValidationHelper)
        {
            _context = context;
            _doctorValidationHelper = doctorValidationHelper;
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
                var existingDoctor = await _context.Doctors
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d => d.DoctorID == request.DoctorId, cancellationToken);
                if (existingDoctor == null)
                {
                    response.Success = false;
                    response.Message = "Invalid doctor Id";

                    return response;
                }

                var existingHospital = await _context.Hospitals
                    .AsNoTracking()
                    .FirstOrDefaultAsync(h => h.HospitalID == request.HospitalId, cancellationToken);
                if (existingHospital == null)
                {
                    response.Success = false;
                    response.Message = "Invalid hospital Id";

                    return response;
                }

                if (!await _doctorValidationHelper.ValidateDoctorAsync(request.HospitalId, request.DoctorId, cancellationToken))
                {
                    response.Message = "Doctor is not associated with the specified hospital.";
                    return response;
                }

                var existingPatient = await _context.PatientRegistrations
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.PatientId == request.PatientId && p.HospitalId == request.HospitalId, cancellationToken);
                if (existingPatient == null)
                {
                    response.Success = false;
                    response.Message = "Invalid patient Id";

                    return response;
                }

                var existingAppointment = await _context.Appointments
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.ApptId == request.AppointmentId && a.HospitalId == request.HospitalId && a.DoctorId == request.DoctorId, cancellationToken);
                if (existingAppointment == null)
                {
                    response.Success = false;
                    response.Message = "Invalid appointment Id or appointment Id, hospital Id, doctor Id combination does not exists";

                    return response;
                }

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
                        Mobile = patientRegistration.Mobile,
                        Age = patientRegistration.AgeYears ?? 0,
                        Sex = patientRegistration.Sex,
                        Address = patientRegistration.AddressLine,
                        City = patientRegistration.City,
                        State = patientRegistration.State,
                        Country = patientRegistration.Country,
                        Pincode = patientRegistration.Pincode,
                        InsuranceId = patientRegistration.InsuranceId
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
                response.Message = "Prescription details generated successfully.";
            }
            catch(Exception ex)
            {
                response.Success = false;
                response.Message = "An error occurred while generating the prescription." + ex.Message;
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
                        Sys = bpElement.TryGetProperty("Sys", out var sys) ? sys.GetDouble() : 0,
                        Dia = bpElement.TryGetProperty("Dia", out var dia) ? dia.GetDouble() : 0
                    };
                }

                if (vitalsJson.TryGetProperty("Pulse", out var pulse))
                    model.Pulse = pulse.GetDouble();

                if (vitalsJson.TryGetProperty("TempC", out var tempC))
                    model.TempC = tempC.GetDouble();

                if (vitalsJson.TryGetProperty("Spo2", out var spo2))
                    model.Spo2 = spo2.GetDouble();

                if (vitalsJson.TryGetProperty("HeightCm", out var heightCm))
                    model.HeightCm = heightCm.GetDouble();

                if (vitalsJson.TryGetProperty("WeightKg", out var weightKg))
                    model.WeightKg = weightKg.GetDouble();

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
