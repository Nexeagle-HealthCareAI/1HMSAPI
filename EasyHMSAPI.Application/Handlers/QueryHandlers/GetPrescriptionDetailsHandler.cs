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
    public class GetPrescriptionDetailsHandler : IRequestHandler<GetPrescriptionDetailsRequestModel, GetPrescriptionDetailsResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IDoctorValidationHelper _doctorValidationHelper;

        public GetPrescriptionDetailsHandler(AppDbContext context, IDoctorValidationHelper doctorValidationHelper)
        {
            _context = context;
            _doctorValidationHelper = doctorValidationHelper;
        }

        public async Task<GetPrescriptionDetailsResponseModel> Handle(GetPrescriptionDetailsRequestModel request, CancellationToken cancellationToken)
        {
            GetPrescriptionDetailsResponseModel response = new()
            {
                Success = false
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

                var existingAppointment = await _context.Appointments
                    .Where(x => x.ApptId == request.AppointmentId)
                    .FirstOrDefaultAsync(cancellationToken);
                if (existingAppointment == null)
                {
                    response.Message = "Appointment not found.";
                }
                else
                {
                    var prescriptionDetails = await _context.Prescription
                        .Where(p => p.ApptId == request.AppointmentId
                            && p.DoctorId == request.DoctorId
                            && p.HospitalId == request.HospitalId
                            && p.PatientId == request.PatientId)
                        .FirstOrDefaultAsync(cancellationToken);
                    var vitals = await _context.AppointmentVitals
                            .Where(v => v.ApptId == request.AppointmentId)
                            .Select(v => v.VitalsJson)
                            .FirstOrDefaultAsync(cancellationToken);

                    if (prescriptionDetails is not null)
                    {
                        var prescriptionInvestigation = await _context.PrescriptionInvestigation
                            .Where(x => x.PrescriptionId == prescriptionDetails.PrescriptionId
                                        && x.OrdersType == AppConstants.LookupType_Investigation)
                            .FirstOrDefaultAsync(cancellationToken);
                        var prescriptionProcedure = await _context.PrescriptionInvestigation
                           .Where(x => x.PrescriptionId == prescriptionDetails.PrescriptionId
                                       && x.OrdersType == AppConstants.LookupType_Procedure)
                           .FirstOrDefaultAsync(cancellationToken);
                        var prescriptionMedicines = await _context.PrescriptionMedicine
                            .Where(x => x.PrescriptionId == prescriptionDetails.PrescriptionId)
                            .ToListAsync(cancellationToken);

                        PrescriptionDetailsDataModel prescriptionDetailsDataModel = new()
                        {
                            PrescriptionId = prescriptionDetails.PrescriptionId,
                            AppointmentId = request.AppointmentId,
                            PatientId = request.PatientId,
                            DoctorId = request.DoctorId,
                            HospitalId = request.HospitalId,
                            VitalsJson = SafeDeserialize<PatientVitalsModel>(vitals),
                            ChiefComplaint = prescriptionDetails.ChiefComplaint,
                            History = prescriptionDetails.History,
                            Comorbidity = prescriptionDetails.Comorbidity,
                            Examination = prescriptionDetails.Examination,
                            SystemicExamination = prescriptionDetails.SystemicExamination,
                            Diagnosis = prescriptionDetails.Diagnosis,
                            Orders = new OrdersModel
                            {
                                Investigations = prescriptionInvestigation?.Name is not null
                                                ? prescriptionInvestigation.Name.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries).ToList()
                                                : null,
                                Procedures = prescriptionProcedure?.Name is not null
                                                ? prescriptionProcedure.Name.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries).ToList()
                                                : null
                            },
                            Medications = prescriptionMedicines?.Count > 0
                                          ? prescriptionMedicines.Select(m => new MedicationModel
                                          {
                                              DrugName = m.MedicineName,
                                              Dose = m.Dosage,
                                              Route = m.Route,
                                              Frequency = m.Frequency,
                                              Duration = m.Durations,
                                              Instructions = m.Instructions,
                                              SaltName = m.SaltName,
                                              DisplayOrder = m.DisplayOrder
                                          }).ToList()
                                          : null,
                            NonPharmacologicalAdvice = SafeDeserialize<List<NonPharmacologicalAdviceModel>>(prescriptionDetails.NonPharmacologicalAdvice),
                            PrivateNotes = prescriptionDetails.PrivateNotes,
                            Certificates = SafeDeserialize<CertificateDataModel>(prescriptionDetails.CertificatesAndNotes),
                            FollowUp = new FollowUpModel
                            {
                                FollowUpOn = prescriptionDetails.FollowUpDate,
                                Reason = SafeDeserialize<FollowupReasonModel>(prescriptionDetails.FollowUpNotes),
                                Referral = SafeDeserialize<ReferralModel>(prescriptionDetails.Referral)
                            },
                            Immunizations = SafeDeserialize<List<ImmunizationModel>>(prescriptionDetails.Immunizations),
                            CustomFields = ExtractCustomFields(prescriptionDetails.MetaJson)
                        };

                        response.Data = prescriptionDetailsDataModel;
                        response.Success = true;
                        response.Message = "Prescription details retrieved successfully.";

                    }
                    else
                    {
                        if(vitals is null)
                        {
                            response.Message = "No prescription details or vitals found for the given appointment.";
                        }
                        else
                        {
                            PrescriptionDetailsDataModel prescriptionDetailsDataModel = new()
                            {
                                AppointmentId = request.AppointmentId,
                                PatientId = request.PatientId,
                                DoctorId = request.DoctorId,
                                HospitalId = request.HospitalId,
                                VitalsJson = SafeDeserialize<PatientVitalsModel>(vitals)
                            };

                            response.Data = prescriptionDetailsDataModel;
                            response.Success = true;
                            response.Message = "Vitals data retrieved successfully.";
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                response.Message = ex.Message + ex.InnerException + ex.StackTrace;
            }

            return response;
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

        // Pulls the doctor's custom fields out of MetaJson (shape: { "customFields": [ {key,label,value} ] }).
        private static List<PrescriptionCustomFieldModel>? ExtractCustomFields(string? metaJson)
        {
            if (string.IsNullOrWhiteSpace(metaJson)) return null;
            try
            {
                var wrapper = JsonSerializer.Deserialize<MetaJsonWrapper>(metaJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return wrapper?.CustomFields != null && wrapper.CustomFields.Count > 0 ? wrapper.CustomFields : null;
            }
            catch { /* ignore malformed MetaJson */ }
            return null;
        }

        private class MetaJsonWrapper
        {
            public List<PrescriptionCustomFieldModel>? CustomFields { get; set; }
        }
    }
}
