using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>
    /// Save (repeatable upsert) and Sign (one-way lock) for a discharge summary. Both enforce the
    /// same "no edits once signed" invariant — Save defensively, Sign by definition.
    /// </summary>
    public class DischargeSummaryCommandHandlers :
        IRequestHandler<SaveDischargeSummaryRequestModel, SaveDischargeSummaryResponseModel>,
        IRequestHandler<SignDischargeSummaryRequestModel, SignDischargeSummaryResponseModel>,
        IRequestHandler<UnsignDischargeSummaryRequestModel, UnsignDischargeSummaryResponseModel>
    {
        private readonly AppDbContext _context;

        public DischargeSummaryCommandHandlers(AppDbContext context)
        {
            _context = context;
        }

        public async Task<SaveDischargeSummaryResponseModel> Handle(SaveDischargeSummaryRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                    return new SaveDischargeSummaryResponseModel { Success = false, Message = "HospitalId and AdmissionId are required." };

                var condition = string.IsNullOrWhiteSpace(request.ConditionAtDischarge) ? null : request.ConditionAtDischarge.Trim().ToUpperInvariant();
                if (condition != null && !IpdConstants.ConditionAtDischarge.All.Contains(condition))
                    return new SaveDischargeSummaryResponseModel { Success = false, Message = "Invalid condition at discharge." };

                var admission = await _context.Admission
                    .FirstOrDefaultAsync(a => a.AdmissionId == request.AdmissionId && a.HospitalId == request.HospitalId, cancellationToken);
                if (admission == null)
                    return new SaveDischargeSummaryResponseModel { Success = false, Message = "Admission not found." };

                var existing = await _context.DischargeSummary
                    .FirstOrDefaultAsync(d => d.HospitalId == request.HospitalId && d.AdmissionId == request.AdmissionId, cancellationToken);

                var now = DateTime.UtcNow;
                if (existing != null)
                {
                    if (existing.IsSigned)
                        return new SaveDischargeSummaryResponseModel { Success = false, Message = "Discharge summary is already signed and locked." };

                    existing.AdmittingDiagnosis = request.AdmittingDiagnosis;
                    existing.FinalDiagnosis = request.FinalDiagnosis;
                    existing.ChiefComplaint = request.ChiefComplaint;
                    existing.HistoryOfPresentIllness = request.HistoryOfPresentIllness;
                    existing.CourseInHospital = request.CourseInHospital;
                    existing.ProceduresPerformed = request.ProceduresPerformed;
                    existing.ConditionAtDischarge = condition;
                    existing.DischargeMedications = request.Medications != null
                        ? DischargeSummaryComposer.ComposeDischargeMedicationsText(request.Medications.Select(ToMedTuple))
                        : request.DischargeMedications;
                    existing.FollowUpInstructions = request.FollowUpInstructions;
                    existing.FollowUpDate = request.FollowUpDate;
                    existing.DietInstructions = request.DietInstructions;
                    existing.ActivityRestrictions = request.ActivityRestrictions;
                    existing.AdditionalNotes = request.AdditionalNotes;
                    existing.UpdatedAt = now;
                    existing.UpdatedBy = request.LoggedInUserName;

                    await SyncMedicationsAsync(existing.DischargeSummaryId, request.Medications, cancellationToken);
                    await _context.SaveChangesAsync(cancellationToken);
                    return new SaveDischargeSummaryResponseModel { Success = true, Message = "Discharge summary saved.", DischargeSummaryId = existing.DischargeSummaryId };
                }

                var summary = new DischargeSummary
                {
                    DischargeSummaryId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    AdmissionId = admission.AdmissionId,
                    EncounterId = admission.EncounterId,
                    PatientId = admission.PatientId,
                    AdmittingDiagnosis = request.AdmittingDiagnosis,
                    FinalDiagnosis = request.FinalDiagnosis,
                    ChiefComplaint = request.ChiefComplaint,
                    HistoryOfPresentIllness = request.HistoryOfPresentIllness,
                    CourseInHospital = request.CourseInHospital,
                    ProceduresPerformed = request.ProceduresPerformed,
                    ConditionAtDischarge = condition,
                    DischargeMedications = request.Medications != null
                        ? DischargeSummaryComposer.ComposeDischargeMedicationsText(request.Medications.Select(ToMedTuple))
                        : request.DischargeMedications,
                    FollowUpInstructions = request.FollowUpInstructions,
                    FollowUpDate = request.FollowUpDate,
                    DietInstructions = request.DietInstructions,
                    ActivityRestrictions = request.ActivityRestrictions,
                    AdditionalNotes = request.AdditionalNotes,
                    IsSigned = false,
                    CreatedAt = now,
                    CreatedBy = request.LoggedInUserName,
                    UpdatedAt = now,
                    UpdatedBy = request.LoggedInUserName,
                };
                _context.DischargeSummary.Add(summary);

                await SyncMedicationsAsync(summary.DischargeSummaryId, request.Medications, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                return new SaveDischargeSummaryResponseModel { Success = true, Message = "Discharge summary saved.", DischargeSummaryId = summary.DischargeSummaryId };
            }
            catch (Exception)
            {
                return new SaveDischargeSummaryResponseModel { Success = false, Message = "Error saving discharge summary." };
            }
        }

        private static (string? Name, string? Dose, string? Route, string? Frequency, string? Duration, string? Instructions) ToMedTuple(DischargeMedicationModel m) =>
            (m.MedicineName, m.Dosage, m.Route, m.Frequency, m.Durations, m.Instructions);

        // Full delete-and-reinsert per save — same pattern SavePrescriptionDetailsHandler uses for
        // PrescriptionMedicine, no per-row diffing.
        private async Task SyncMedicationsAsync(Guid dischargeSummaryId, List<DischargeMedicationModel>? medications, CancellationToken cancellationToken)
        {
            if (medications == null) return;

            var existingLines = await _context.DischargeMedication
                .Where(m => m.DischargeSummaryId == dischargeSummaryId)
                .ToListAsync(cancellationToken);
            _context.DischargeMedication.RemoveRange(existingLines);

            var now = DateTime.UtcNow;
            for (var i = 0; i < medications.Count; i++)
            {
                var m = medications[i];
                _context.DischargeMedication.Add(new DischargeMedication
                {
                    DischargeMedicationId = Guid.NewGuid(),
                    DischargeSummaryId = dischargeSummaryId,
                    MedicineName = m.MedicineName,
                    Dosage = m.Dosage,
                    Route = m.Route,
                    Frequency = m.Frequency,
                    Durations = m.Durations,
                    Instructions = m.Instructions,
                    SaltName = m.SaltName,
                    DisplayOrder = m.DisplayOrder ?? i,
                    CreatedAt = now,
                    UpdatedAt = now,
                });
            }
        }

        public async Task<SignDischargeSummaryResponseModel> Handle(SignDischargeSummaryRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                    return new SignDischargeSummaryResponseModel { Success = false, Message = "HospitalId and AdmissionId are required." };

                var summary = await _context.DischargeSummary
                    .FirstOrDefaultAsync(d => d.HospitalId == request.HospitalId && d.AdmissionId == request.AdmissionId, cancellationToken);
                if (summary == null)
                    return new SignDischargeSummaryResponseModel { Success = false, Message = "Save the discharge summary before signing." };
                if (summary.IsSigned)
                    return new SignDischargeSummaryResponseModel { Success = false, Message = "Discharge summary is already signed." };
                if (string.IsNullOrWhiteSpace(summary.FinalDiagnosis) || string.IsNullOrWhiteSpace(summary.ConditionAtDischarge))
                    return new SignDischargeSummaryResponseModel { Success = false, Message = "Final diagnosis and condition at discharge are required before signing." };

                var now = DateTime.UtcNow;
                summary.IsSigned = true;
                summary.SignedAt = now;
                summary.SignedBy = request.LoggedInUserName;
                summary.SignedByDoctorId = request.LoggedInUserId;
                summary.SignedByDoctorName = string.IsNullOrWhiteSpace(request.DoctorName) ? request.LoggedInUserName : request.DoctorName.Trim();
                summary.UpdatedAt = now;
                summary.UpdatedBy = request.LoggedInUserName;

                await _context.SaveChangesAsync(cancellationToken);
                return new SignDischargeSummaryResponseModel { Success = true, Message = "Discharge summary signed." };
            }
            catch (Exception)
            {
                return new SignDischargeSummaryResponseModel { Success = false, Message = "Error signing discharge summary." };
            }
        }

        public async Task<UnsignDischargeSummaryResponseModel> Handle(UnsignDischargeSummaryRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                    return new UnsignDischargeSummaryResponseModel { Success = false, Message = "HospitalId and AdmissionId are required." };

                var summary = await _context.DischargeSummary
                    .FirstOrDefaultAsync(d => d.HospitalId == request.HospitalId && d.AdmissionId == request.AdmissionId, cancellationToken);
                if (summary == null)
                    return new UnsignDischargeSummaryResponseModel { Success = false, Message = "Discharge summary not found." };
                if (!summary.IsSigned)
                    return new UnsignDischargeSummaryResponseModel { Success = false, Message = "Discharge summary is not signed." };

                var now = DateTime.UtcNow;
                summary.IsSigned = false;
                summary.SignedAt = null;
                summary.SignedBy = null;
                summary.SignedByDoctorId = null;
                summary.SignedByDoctorName = null;
                summary.UpdatedAt = now;
                summary.UpdatedBy = request.LoggedInUserName;

                await _context.SaveChangesAsync(cancellationToken);
                return new UnsignDischargeSummaryResponseModel { Success = true, Message = "Discharge summary signature revoked." };
            }
            catch (Exception)
            {
                return new UnsignDischargeSummaryResponseModel { Success = false, Message = "Error unsigning discharge summary." };
            }
        }
    }
}
