using EasyHMSAPI.Application.Helpers.Implementations;
using EasyHMSAPI.Application.Helpers.Interfaces;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetPatientTimelineHandler : IRequestHandler<GetPatientTimelineRequestModel, GetPatientTimelineResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IDoctorValidationHelper _doctorValidationHelper;

        public GetPatientTimelineHandler(AppDbContext context, IDoctorValidationHelper doctorValidationHelper)
        {
            _context = context;
            _doctorValidationHelper = doctorValidationHelper;
        }

        public async Task<GetPatientTimelineResponseModel> Handle(GetPatientTimelineRequestModel request, CancellationToken cancellationToken)
        {
            GetPatientTimelineResponseModel response = new()
            {
                Success = false,
                Data = new List<PatientTimelineDataModel>()
            };

            try
            {
                var existingDoctor = await _context.Doctors
                 .Where(x => x.DoctorID == request.DoctorId)
                 .AsNoTracking()
                 .FirstOrDefaultAsync(cancellationToken);
                if (existingDoctor == null)
                {
                    response.Message = "Doctor not found.";
                    return response;
                }

                var existingHospital = await _context.Hospitals
                    .Where(x => x.HospitalID == request.HospitalId)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(cancellationToken);
                if (existingHospital == null)
                {
                    response.Message = "Hospital not found.";
                    return response;
                }

                if (!await _doctorValidationHelper.ValidateDoctorAsync(request.HospitalId, request.DoctorId, cancellationToken))
                {
                    response.Message = "Doctor is not associated with the specified hospital.";
                    return response;
                }

                // Get all appointments for the patient with the doctor
                var appointments = await _context.Appointments
                    .Where(a => a.PatientId == request.PatientId 
                        && a.DoctorId == request.DoctorId 
                        && a.HospitalId == request.HospitalId)
                    .AsNoTracking()
                    .OrderByDescending(a => a.ApptDate)
                    .ToListAsync(cancellationToken);

                if (appointments.Count == 0)
                {
                    response.Success = true;
                    response.Message = "No appointments found for the patient.";
                    return response;
                }

                var timelineDataModel = new PatientTimelineDataModel
                {
                    PatientID = request.PatientId,
                    HospitalId = request.HospitalId,
                    DoctorId = request.DoctorId,
                    TimelineData = new List<TimelineAppointmentModel>()
                };

                foreach (var appointment in appointments)
                {
                    var timelineAppointment = new TimelineAppointmentModel
                    {
                        ApptID = appointment.ApptId,
                        AppDate = appointment.ApptDate,
                        Status = appointment.CurrentStatusCode,
                        StatusJsonHistory = new List<StatusHistoryModel>()
                    };

                    // Parse status history
                    if (!string.IsNullOrWhiteSpace(appointment.StatusHistoryJson))
                    {
                        try
                        {
                            var statusHistory = JsonSerializer.Deserialize<List<StatusHistoryModel>>(appointment.StatusHistoryJson);
                            timelineAppointment.StatusJsonHistory = statusHistory ?? new List<StatusHistoryModel>();
                        }
                        catch
                        {
                            timelineAppointment.StatusJsonHistory = new List<StatusHistoryModel>();
                        }
                    }

                    // Fetch vitals
                    var appointmentVitals = await _context.AppointmentVitals
                        .AsNoTracking()
                        .FirstOrDefaultAsync(v => v.ApptId == appointment.ApptId 
                            && v.PatientId == request.PatientId, cancellationToken);

                    if (appointmentVitals != null && !string.IsNullOrWhiteSpace(appointmentVitals.VitalsJson))
                    {
                        try
                        {
                            var vitalsJson = JsonSerializer.Deserialize<JsonElement>(appointmentVitals.VitalsJson);
                            timelineAppointment.VitalsJson = ParseVitalsFromJson(vitalsJson);
                        }
                        catch
                        {
                        }
                    }

                    // Fetch prescription details
                    var prescriptionDetails = await _context.Prescription
                        .AsNoTracking()
                        .FirstOrDefaultAsync(p => p.ApptId == appointment.ApptId
                            && p.DoctorId == request.DoctorId
                            && p.HospitalId == request.HospitalId
                            && p.PatientId == request.PatientId, cancellationToken);

                    if (prescriptionDetails != null)
                    {
                        timelineAppointment.ChiefComplaint = prescriptionDetails.ChiefComplaint;
                        timelineAppointment.History = prescriptionDetails.History;
                        timelineAppointment.Comorbidity = prescriptionDetails.Comorbidity;
                        timelineAppointment.Examination = prescriptionDetails.Examination;
                        timelineAppointment.Diagnosis = prescriptionDetails.Diagnosis;
                        timelineAppointment.PrivateNotes = prescriptionDetails.PrivateNotes;

                        // Fetch investigations
                        var prescriptionInvestigation = await _context.PrescriptionInvestigation
                            .AsNoTracking()
                            .FirstOrDefaultAsync(x => x.PrescriptionId == prescriptionDetails.PrescriptionId
                                && x.OrdersType == AppConstants.LookupType_Investigation, cancellationToken);

                        // Fetch procedures
                        var prescriptionProcedure = await _context.PrescriptionInvestigation
                            .AsNoTracking()
                            .FirstOrDefaultAsync(x => x.PrescriptionId == prescriptionDetails.PrescriptionId
                                && x.OrdersType == AppConstants.LookupType_Procedure, cancellationToken);

                        // Build Orders model
                        timelineAppointment.Orders = new OrdersModel
                        {
                            Investigations = prescriptionInvestigation?.Name is not null
                                ? prescriptionInvestigation.Name.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries).ToList()
                                : null,
                            Procedures = prescriptionProcedure?.Name is not null
                                ? prescriptionProcedure.Name.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries).ToList()
                                : null
                        };

                        // Fetch all medicines
                        var prescriptionMedicines = await _context.PrescriptionMedicine
                            .AsNoTracking()
                            .Where(x => x.PrescriptionId == prescriptionDetails.PrescriptionId)
                            .ToListAsync(cancellationToken);

                        // Build Medications model
                        if (prescriptionMedicines?.Count > 0)
                        {
                            timelineAppointment.Medications = prescriptionMedicines.Select(m => new MedicationModel
                            {
                                DrugName = m.MedicineName,
                                Dose = m.Dosage,
                                Route = m.Route,
                                Frequency = m.Frequency,
                                Duration = m.Durations,
                                Instructions = m.Instructions,
                                SaltName = m.SaltName
                            }).ToList();
                        }

                        // Parse NonPharmacologicalAdvice
                        if (!string.IsNullOrWhiteSpace(prescriptionDetails.NonPharmacologicalAdvice))
                        {
                            timelineAppointment.NonPharmacologicalAdvice = SafeDeserialize<List<NonPharmacologicalAdviceModel>>(prescriptionDetails.NonPharmacologicalAdvice);
                        }

                        // Parse Certificates
                        if (!string.IsNullOrWhiteSpace(prescriptionDetails.CertificatesAndNotes))
                        {
                            timelineAppointment.Certificates = SafeDeserialize<CertificateDataModel>(prescriptionDetails.CertificatesAndNotes);
                        }

                        // Build FollowUp model
                        ReferralModel? referralModel = null;
                        if (!string.IsNullOrWhiteSpace(prescriptionDetails.Referral))
                        {
                            referralModel = SafeDeserialize<ReferralModel>(prescriptionDetails.Referral);
                        }

                        timelineAppointment.FollowUp = new FollowUpModel
                        {
                            FollowUpOn = prescriptionDetails.FollowUpDate,
                            Reason = prescriptionDetails.FollowUpNotes,
                            Referral = referralModel,
                            ReferralEnabled = prescriptionDetails.Referral is not null
                        };

                        // Parse Immunizations
                        if (!string.IsNullOrWhiteSpace(prescriptionDetails.Immunizations))
                        {
                            timelineAppointment.Immunizations = SafeDeserialize<List<ImmunizationModel>>(prescriptionDetails.Immunizations);
                        }
                    }

                    // Fetch attachments
                    var attachments = await _context.PrescriptionAttachments
                        .AsNoTracking()
                        .Where(a => a.ApptId == appointment.ApptId 
                            && a.PatientId == request.PatientId)
                        .ToListAsync(cancellationToken);

                    if (attachments?.Count > 0)
                    {
                        timelineAppointment.Attachments = attachments.Select(a => new AttachmentModel
                        {
                            AttachmentId = a.AttachmentId,
                            ReportType = a.ReportType,
                            FileName = a.FileName,
                            StorageUrl = a.StorageUrl,
                            Notes = a.Notes,
                            UploadedAt = a.UploadedAt,
                            UploadedBy = a.UploadedBy
                        }).ToList();
                    }

                    timelineDataModel.TimelineData.Add(timelineAppointment);
                }

                response.Data = new List<PatientTimelineDataModel> { timelineDataModel };
                response.Success = true;
                response.Message = "Patient timeline retrieved successfully.";
            }
            catch(Exception ex)
            {
                response.Success = false;
                response.Message = "An error occurred while retrieving the patient timeline." + ex.Message;
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

        private T? SafeDeserialize<T>(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return default;

            try
            {
                return JsonSerializer.Deserialize<T>(json);
            }
            catch (JsonException)
            {
                return default;
            }
        }
    }
}
