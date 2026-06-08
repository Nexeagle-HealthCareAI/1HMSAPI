using EasyHMSAPI.Application.Helpers.Interfaces;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class SavePrescriptionDetailsHandler : IRequestHandler<SavePrescriptionDetailsRequestModel, SavePrescriptionDetailsResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IDoctorValidationHelper _doctorValidationHelper;
        private readonly IBlobStorageService _blobStorageService;
        private readonly string _containerName;

        public SavePrescriptionDetailsHandler(AppDbContext context, IDoctorValidationHelper doctorValidationHelper, IBlobStorageService blobStorageService, IConfiguration configuration)
        {
            _context = context;
            _doctorValidationHelper = doctorValidationHelper;
            _blobStorageService = blobStorageService;
            _containerName = configuration["BlobStorage:PrescriptionsContainer"] ?? string.Empty;
        }

        public async Task<SavePrescriptionDetailsResponseModel> Handle(SavePrescriptionDetailsRequestModel request, CancellationToken cancellationToken)
        {
            SavePrescriptionDetailsResponseModel response = new()
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
                    if (request.PrescriptionId is not null)
                    {
                        var existingPrescription = await _context.Prescription
                            .Where(x => x.PrescriptionId == request.PrescriptionId)
                            .FirstOrDefaultAsync(cancellationToken);
                        if (existingPrescription == null)
                        {
                            response.Message = "Prescription not found.";
                        }
                        else
                        {
                            if (request.VitalsJson is not null)
                            {
                                var appointmentVitals = await _context.AppointmentVitals
                                    .Where(x => x.ApptId == request.AppointmentId)
                                    .FirstOrDefaultAsync(cancellationToken);
                                if (appointmentVitals is not null)
                                {
                                    appointmentVitals.VitalsJson = JsonSerializer.Serialize(request.VitalsJson);
                                    appointmentVitals.RecordedAt = request.CurrentDateTime;
                                    appointmentVitals.RecordedBy = request.LoggedInUserId;
                                }
                                else
                                {
                                    AppointmentVitals newVitals = new()
                                    {
                                        VitalId = Guid.NewGuid(),
                                        HospitalId = request.HospitalId,
                                        PatientId = request.PatientId ?? string.Empty,
                                        ApptId = request.AppointmentId,
                                        VitalsJson = JsonSerializer.Serialize(request.VitalsJson),
                                        RecordedAt = request.CurrentDateTime,
                                        RecordedBy = request.LoggedInUserId
                                    };
                                    _context.AppointmentVitals.Add(newVitals);
                                }
                            }
                            if (!string.IsNullOrEmpty(request.ChiefComplaint)) existingPrescription.ChiefComplaint = request.ChiefComplaint;
                            if (!string.IsNullOrEmpty(request.History)) existingPrescription.History = request.History;
                            if (!string.IsNullOrEmpty(request.Comorbidity)) existingPrescription.Comorbidity = request.Comorbidity;
                            if (!string.IsNullOrEmpty(request.Examination)) existingPrescription.Examination = request.Examination;
                            if (!string.IsNullOrEmpty(request.Diagnosis)) existingPrescription.Diagnosis = request.Diagnosis;
                            if (request.Orders is null)
                            {
                                var existingItems = await _context.PrescriptionInvestigation
                                    .Where(x => x.PrescriptionId == request.PrescriptionId)
                                    .ToListAsync(cancellationToken);
                                if (existingItems is null)
                                {
                                    if (existingAppointment.CurrentStatusCode == AppConstants.AppointmentStatus_VitalsRequired || existingAppointment.CurrentStatusCode == AppConstants.AppointmentStatus_Ready)
                                    {
                                        existingAppointment.CurrentStatusCode = AppConstants.AppointmentStatus_UnderConsult;
                                        existingAppointment.LastStatusCodeAt = request.CurrentDateTime;
                                        var history = string.IsNullOrEmpty(existingAppointment.StatusHistoryJson)
                                        ? new List<object>()
                                        : JsonSerializer.Deserialize<List<object>>(existingAppointment.StatusHistoryJson) ?? new List<object>();
                                        history.Add(new { status = AppConstants.AppointmentStatus_UnderConsult, timestamp = request.CurrentDateTime });
                                        existingAppointment.StatusHistoryJson = JsonSerializer.Serialize(history);
                                    }
                                }
                            }
                            if (request.Orders is not null)
                            {
                                if (existingAppointment.CurrentStatusCode == AppConstants.AppointmentStatus_VitalsRequired || existingAppointment.CurrentStatusCode == AppConstants.AppointmentStatus_Ready || existingAppointment.CurrentStatusCode == AppConstants.AppointmentStatus_UnderConsult)
                                {
                                    existingAppointment.CurrentStatusCode = AppConstants.AppointmentStatus_LabRequired;
                                    existingAppointment.LastStatusCodeAt = request.CurrentDateTime;
                                    var history = string.IsNullOrEmpty(existingAppointment.StatusHistoryJson)
                                    ? new List<object>()
                                    : JsonSerializer.Deserialize<List<object>>(existingAppointment.StatusHistoryJson) ?? new List<object>();
                                    history.Add(new { status = AppConstants.AppointmentStatus_LabRequired, timestamp = request.CurrentDateTime });
                                    existingAppointment.StatusHistoryJson = JsonSerializer.Serialize(history);
                                }

                                var existingItems= await _context.PrescriptionInvestigation
                                    .Where(x => x.PrescriptionId == request.PrescriptionId)
                                    .ToListAsync(cancellationToken);
                                if(existingItems is not null)
                                {
                                    _context.PrescriptionInvestigation.RemoveRange(existingItems);
                                }
                                else
                                {
                                    if (existingAppointment.CurrentStatusCode == AppConstants.AppointmentStatus_VitalsRequired || existingAppointment.CurrentStatusCode == AppConstants.AppointmentStatus_Ready || existingAppointment.CurrentStatusCode == AppConstants.AppointmentStatus_UnderConsult)
                                    {
                                        existingAppointment.CurrentStatusCode = AppConstants.AppointmentStatus_LabRequired;
                                        existingAppointment.LastStatusCodeAt = request.CurrentDateTime;
                                        var history = string.IsNullOrEmpty(existingAppointment.StatusHistoryJson)
                                        ? new List<object>()
                                        : JsonSerializer.Deserialize<List<object>>(existingAppointment.StatusHistoryJson) ?? new List<object>();
                                        history.Add(new { status = AppConstants.AppointmentStatus_LabRequired, timestamp = request.CurrentDateTime });
                                        existingAppointment.StatusHistoryJson = JsonSerializer.Serialize(history);
                                        existingPrescription.Status = AppConstants.AppointmentStatus_LabRequired;
                                    }
                                }

                                if (request.Orders.Investigations is not null)
                                {
                                    var investigationLookup = await _context.LookupTypes
                                        .Where(x => x.LookupTypeCode == AppConstants.LookupType_Investigation)
                                        .Select(x => new
                                        {
                                            x.LookupTypeId,
                                            x.LookupTypeCode
                                        })
                                        .FirstOrDefaultAsync(cancellationToken);
                                    PrescriptionInvestigation investigation = new()
                                    {
                                        PresInvestigationId = Guid.NewGuid(),
                                        PrescriptionId = existingPrescription.PrescriptionId,
                                        LookupTypeId = investigationLookup is not null ? investigationLookup.LookupTypeId : 0,
                                        OrdersType = investigationLookup is not null ? investigationLookup.LookupTypeCode : string.Empty,
                                        Name = string.Join(", ", request.Orders.Investigations),
                                        CreatedAt = request.CurrentDateTime,
                                        UpdatedAt = request.CurrentDateTime,
                                        UpdateBy = request.LoggedInUserName,
                                    };
                                    _context.PrescriptionInvestigation.Add(investigation);
                                }

                                if(request.Orders.Procedures is not null)
                                {
                                    var medicationLookup = await _context.LookupTypes
                                        .Where(x => x.LookupTypeCode == AppConstants.LookupType_Procedure)
                                        .Select(x => new
                                        {
                                            x.LookupTypeId,
                                            x.LookupTypeCode
                                        })
                                        .FirstOrDefaultAsync(cancellationToken);
                                    PrescriptionInvestigation medication = new()
                                    {
                                        PresInvestigationId = Guid.NewGuid(),
                                        PrescriptionId = existingPrescription.PrescriptionId,
                                        LookupTypeId = medicationLookup is not null ? medicationLookup.LookupTypeId : 0,
                                        OrdersType = medicationLookup is not null ? medicationLookup.LookupTypeCode : string.Empty,
                                        Name = string.Join(", ", request.Orders.Procedures),
                                        CreatedAt = request.CurrentDateTime,
                                        UpdatedAt = request.CurrentDateTime,
                                        UpdateBy = request.LoggedInUserName,
                                    };
                                    _context.PrescriptionInvestigation.Add(medication);
                                }
                            }
                            if (request.Medications is not null)
                            {
                                var existingMedications = await _context.PrescriptionMedicine
                                    .Where(x => x.PrescriptionId == request.PrescriptionId)
                                    .ToListAsync(cancellationToken);
                                if (existingMedications is not null)
                                {
                                    _context.PrescriptionMedicine.RemoveRange(existingMedications);
                                }

                                foreach (var med in request.Medications)
                                {
                                    PrescriptionMedicine prescriptionMedicine = new()
                                    {
                                        PresMedicineId = Guid.NewGuid(),
                                        PrescriptionId = existingPrescription.PrescriptionId,
                                        MedicineName = med.DrugName,
                                        Dosage = med.Dose,
                                        Frequency = med.Frequency,
                                        Durations = med.Duration,
                                        Instructions = med.Instructions,
                                        Route = med.Route,
                                        SaltName = med.SaltName,
                                        CreatedAt = request.CurrentDateTime,
                                        UpdatedAt = request.CurrentDateTime,
                                        UpdateBy = request.LoggedInUserName,
                                        DisplayOrder = med.DisplayOrder
                                    };
                                    _context.PrescriptionMedicine.Add(prescriptionMedicine);
                                }
                            }
                            if(request.NonPharmacologicalAdvice is not null) existingPrescription.NonPharmacologicalAdvice = JsonSerializer.Serialize(request.NonPharmacologicalAdvice);
                            if (!string.IsNullOrEmpty(request.PrivateNotes)) existingPrescription.PrivateNotes = request.PrivateNotes;
                            if (request.IsPrintablePrivateNotes.HasValue) existingPrescription.PrivateNotes = request.PrivateNotes + " IsPrintablePrivateNotes: " + request.IsPrintablePrivateNotes.Value.ToString();
                            if (request.Certificates is not null) existingPrescription.CertificatesAndNotes = JsonSerializer.Serialize(request.Certificates);
                            if(request.Immunizations is not null) existingPrescription.Immunizations = JsonSerializer.Serialize(request.Immunizations);
                            if(request.CustomFields is not null) existingPrescription.MetaJson = JsonSerializer.Serialize(new { customFields = request.CustomFields });
                            if(request.FollowUp is not null)
                            {
                                existingPrescription.FollowUpDate = request.FollowUp.FollowUpOn;
                                existingPrescription.FollowUpNotes = request.FollowUp.Reason is not null ? JsonSerializer.Serialize(request.FollowUp.Reason) : null;
                                existingPrescription.Referral = request.FollowUp.Referral is not null ? JsonSerializer.Serialize(request.FollowUp.Referral) : null;
                            }
                            existingPrescription.UpdatedAt = request.CurrentDateTime;
                            if(request.ActionType == AppConstants.Prescription_ActionType_Submit)
                            {
                                existingAppointment.CurrentStatusCode = AppConstants.AppointmentStatus_Completed;
                                existingAppointment.LastStatusCodeAt = request.CurrentDateTime;
                                var history = string.IsNullOrEmpty(existingAppointment.StatusHistoryJson)
                                    ? new List<object>()
                                    : JsonSerializer.Deserialize<List<object>>(existingAppointment.StatusHistoryJson) ?? new List<object>();
                                history.Add(new { status = AppConstants.AppointmentStatus_Completed, timestamp = request.CurrentDateTime });
                                existingAppointment.StatusHistoryJson = JsonSerializer.Serialize(history);
                                existingPrescription.Status = AppConstants.AppointmentStatus_Completed;
                            }

                            await _context.SaveChangesAsync(cancellationToken);

                            response.Success = true;
                            response.Message = "Prescription saved for later.";
                        }
                    }
                    else
                    {
                        var newPrescriptionId = Guid.NewGuid();
                        var status = AppConstants.AppointmentStatus_UnderConsult;

                        if (request.VitalsJson is not null)
                        {
                            var appointmentVitals = await _context.AppointmentVitals
                                .Where(x => x.ApptId == request.AppointmentId)
                                .FirstOrDefaultAsync(cancellationToken);
                            if (appointmentVitals is not null)
                            {
                                appointmentVitals.VitalsJson = JsonSerializer.Serialize(request.VitalsJson);
                                appointmentVitals.RecordedAt = request.CurrentDateTime;
                                appointmentVitals.RecordedBy = request.LoggedInUserId;
                            }
                            else
                            {
                                AppointmentVitals newVitals = new()
                                {
                                    VitalId =  Guid.NewGuid(),
                                    HospitalId = request.HospitalId,
                                    PatientId = request.PatientId ?? string.Empty,
                                    ApptId = request.AppointmentId,
                                    VitalsJson = JsonSerializer.Serialize(request.VitalsJson),
                                    RecordedAt = request.CurrentDateTime,
                                    RecordedBy = request.LoggedInUserId
                                };
                                _context.AppointmentVitals.Add(newVitals);
                            }
                        }
                        if (request.Orders is null)
                        {
                            if (existingAppointment.CurrentStatusCode == AppConstants.AppointmentStatus_VitalsRequired || existingAppointment.CurrentStatusCode == AppConstants.AppointmentStatus_Ready)
                            {
                                existingAppointment.CurrentStatusCode = AppConstants.AppointmentStatus_UnderConsult;
                                existingAppointment.LastStatusCodeAt = request.CurrentDateTime;
                                var history = string.IsNullOrEmpty(existingAppointment.StatusHistoryJson)
                                ? new List<object>()
                                : JsonSerializer.Deserialize<List<object>>(existingAppointment.StatusHistoryJson) ?? new List<object>();
                                history.Add(new { status = AppConstants.AppointmentStatus_UnderConsult, timestamp = request.CurrentDateTime });
                                existingAppointment.StatusHistoryJson = JsonSerializer.Serialize(history);
                                status = AppConstants.AppointmentStatus_UnderConsult;
                            }
                        }
                        if (request.Orders is not null)
                        {
                            if (existingAppointment.CurrentStatusCode == AppConstants.AppointmentStatus_VitalsRequired || existingAppointment.CurrentStatusCode == AppConstants.AppointmentStatus_Ready || existingAppointment.CurrentStatusCode == AppConstants.AppointmentStatus_UnderConsult)
                            {
                                existingAppointment.CurrentStatusCode = AppConstants.AppointmentStatus_LabRequired;
                                existingAppointment.LastStatusCodeAt = request.CurrentDateTime;
                                var history = string.IsNullOrEmpty(existingAppointment.StatusHistoryJson)
                                ? new List<object>()
                                : JsonSerializer.Deserialize<List<object>>(existingAppointment.StatusHistoryJson) ?? new List<object>();
                                history.Add(new { status = AppConstants.AppointmentStatus_LabRequired, timestamp = request.CurrentDateTime });
                                existingAppointment.StatusHistoryJson = JsonSerializer.Serialize(history);
                                status = AppConstants.AppointmentStatus_LabRequired;
                            }

                            if (request.Orders.Investigations is not null)
                            {
                                var investigationLookup = await _context.LookupTypes
                                    .Where(x => x.LookupTypeCode == AppConstants.LookupType_Investigation)
                                    .Select(x => new
                                    {
                                        x.LookupTypeId,
                                        x.LookupTypeCode
                                    })
                                    .FirstOrDefaultAsync(cancellationToken);
                                PrescriptionInvestigation investigation = new()
                                {
                                    PresInvestigationId = Guid.NewGuid(),
                                    PrescriptionId = newPrescriptionId,
                                    LookupTypeId = investigationLookup is not null ? investigationLookup.LookupTypeId : 0,
                                    OrdersType = investigationLookup is not null ? investigationLookup.LookupTypeCode : string.Empty,
                                    Name = string.Join(", ", request.Orders.Investigations),
                                    CreatedAt = request.CurrentDateTime,
                                    UpdatedAt = request.CurrentDateTime,
                                    UpdateBy = request.LoggedInUserName,
                                };
                                _context.PrescriptionInvestigation.Add(investigation);
                            }
                            if (request.Orders.Procedures is not null)
                            {
                                var procedureLookup = await _context.LookupTypes
                                    .Where(x => x.LookupTypeCode == AppConstants.LookupType_Procedure)
                                    .Select(x => new
                                    {
                                        x.LookupTypeId,
                                        x.LookupTypeCode
                                    })
                                    .FirstOrDefaultAsync(cancellationToken);
                                PrescriptionInvestigation procedure = new()
                                {
                                    PresInvestigationId = Guid.NewGuid(),
                                    PrescriptionId = newPrescriptionId,
                                    LookupTypeId = procedureLookup is not null ? procedureLookup.LookupTypeId : 0,
                                    OrdersType = procedureLookup is not null ? procedureLookup.LookupTypeCode : string.Empty,
                                    Name = string.Join(", ", request.Orders.Procedures),
                                    CreatedAt = request.CurrentDateTime,
                                    UpdatedAt = request.CurrentDateTime,
                                    UpdateBy = request.LoggedInUserName,
                                };
                                _context.PrescriptionInvestigation.Add(procedure);
                            }
                        }
                        if(request.Medications is not null)
                        {
                            foreach (var med in request.Medications)
                            {
                                PrescriptionMedicine prescriptionMedicine = new()
                                {
                                    PresMedicineId = Guid.NewGuid(),
                                    PrescriptionId = newPrescriptionId,
                                    MedicineName = med.DrugName,
                                    Dosage = med.Dose,
                                    Frequency = med.Frequency,
                                    Durations = med.Duration,
                                    CreatedAt = request.CurrentDateTime,
                                    UpdatedAt = request.CurrentDateTime,
                                    UpdateBy = request.LoggedInUserName,
                                    Instructions = med.Instructions,
                                    SaltName = med.SaltName,
                                    Route = med.Route,
                                    DisplayOrder = med.DisplayOrder
                                };
                                _context.PrescriptionMedicine.Add(prescriptionMedicine);
                            }
                        }
                        if(request.ActionType == AppConstants.Prescription_ActionType_Submit)
                        {
                            existingAppointment.CurrentStatusCode = AppConstants.AppointmentStatus_Completed;
                            existingAppointment.LastStatusCodeAt = request.CurrentDateTime;
                            var history = string.IsNullOrEmpty(existingAppointment.StatusHistoryJson)
                                    ? new List<object>()
                                    : JsonSerializer.Deserialize<List<object>>(existingAppointment.StatusHistoryJson) ?? new List<object>();
                            history.Add(new { status = AppConstants.AppointmentStatus_Completed, timestamp = request.CurrentDateTime });
                            existingAppointment.StatusHistoryJson = JsonSerializer.Serialize(history);
                            status = AppConstants.AppointmentStatus_Completed;
                        }
                        var newPrescription = new Prescription
                        {
                            PrescriptionId = newPrescriptionId,
                            ApptId = request.AppointmentId,
                            HospitalId = request.HospitalId,
                            DoctorId = request.DoctorId,
                            PatientId = request.PatientId,
                            ChiefComplaint = request.ChiefComplaint,
                            History = request.History,
                            Comorbidity = request.Comorbidity,
                            Examination = request.Examination,
                            Diagnosis = request.Diagnosis,
                            PrivateNotes = request.IsPrintablePrivateNotes.HasValue 
                                ? request.PrivateNotes + " IsPrintablePrivateNotes: " + request.IsPrintablePrivateNotes.Value.ToString() 
                                : request.PrivateNotes,
                            CreatedAt = request.CurrentDateTime,
                            UpdatedAt = request.CurrentDateTime,
                            UpdateBy = request.LoggedInUserName,
                            Status = status,
                            CertificatesAndNotes = request.Certificates is not null ? JsonSerializer.Serialize(request.Certificates) : null,
                            Immunizations = request.Immunizations is not null ? JsonSerializer.Serialize(request.Immunizations) : null,
                            MetaJson = request.CustomFields is not null ? JsonSerializer.Serialize(new { customFields = request.CustomFields }) : null,
                            FollowUpDate = request.FollowUp?.FollowUpOn,
                            FollowUpNotes = request.FollowUp?.Reason is not null ? JsonSerializer.Serialize(request.FollowUp.Reason) : null,
                            Referral = request.FollowUp?.Referral is not null ? JsonSerializer.Serialize(request.FollowUp.Referral) : null,
                            NonPharmacologicalAdvice = request.NonPharmacologicalAdvice is not null ? JsonSerializer.Serialize(request.NonPharmacologicalAdvice) : null,
                        };
                        _context.Prescription.Add(newPrescription);

                        await _context.SaveChangesAsync(cancellationToken);

                        response.Success = true;
                        response.Message = "Prescription saved.";
                    }
                }
            }
            catch (Exception ex)
            {
                response.Message = "An error occurred: " + ex.Message + ex.InnerException + ex.StackTrace;
            }

            return response;
        }
    }
}
