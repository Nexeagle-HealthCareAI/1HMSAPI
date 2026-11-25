using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>
    /// Composes a discharge-summary draft from existing clinical records. Pure query, never
    /// persists — mirrors Voice Rx's own "nothing is saved here, the doctor reviews and applies"
    /// contract, so re-running compose (e.g. after a late round note) never creates duplicate/
    /// stale rows. If a DischargeSummary row already exists for the admission, its saved values
    /// always win over a fresh compose (in-progress edits are never silently overwritten).
    /// Source-material assembly (round notes/procedures/medications) is shared with
    /// GenerateDischargeNarrativeHandler via DischargeSummaryComposer — single source of truth.
    /// </summary>
    public class GetDischargeSummaryDraftHandler : IRequestHandler<GetDischargeSummaryDraftRequestModel, GetDischargeSummaryDraftResponseModel>
    {
        private readonly AppDbContext _context;

        public GetDischargeSummaryDraftHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetDischargeSummaryDraftResponseModel> Handle(GetDischargeSummaryDraftRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                    return new GetDischargeSummaryDraftResponseModel { Success = false, Message = "HospitalId and AdmissionId are required." };

                var admission = await _context.Admission
                    .FirstOrDefaultAsync(a => a.AdmissionId == request.AdmissionId && a.HospitalId == request.HospitalId, cancellationToken);
                if (admission == null)
                    return new GetDischargeSummaryDraftResponseModel { Success = false, Message = "Admission not found." };

                var existing = await _context.DischargeSummary
                    .FirstOrDefaultAsync(d => d.HospitalId == request.HospitalId && d.AdmissionId == request.AdmissionId, cancellationToken);

                if (existing != null)
                {
                    var savedMedications = await _context.DischargeMedication
                        .Where(m => m.DischargeSummaryId == existing.DischargeSummaryId)
                        .OrderBy(m => m.DisplayOrder)
                        .Select(m => new DischargeMedicationModel
                        {
                            MedicineName = m.MedicineName,
                            Dosage = m.Dosage,
                            Route = m.Route,
                            Frequency = m.Frequency,
                            Durations = m.Durations,
                            Instructions = m.Instructions,
                            SaltName = m.SaltName,
                            DisplayOrder = m.DisplayOrder,
                        })
                        .ToListAsync(cancellationToken);

                    return new GetDischargeSummaryDraftResponseModel
                    {
                        Success = true,
                        Draft = new DischargeSummaryDraftModel
                        {
                            DischargeSummaryId = existing.DischargeSummaryId,
                            IsSigned = existing.IsSigned,
                            SignedAt = existing.SignedAt,
                            SignedByDoctorName = existing.SignedByDoctorName,
                            AdmittingDiagnosis = existing.AdmittingDiagnosis,
                            FinalDiagnosis = existing.FinalDiagnosis,
                            FinalDiagnosisIcd10Code = existing.FinalDiagnosisIcd10Code,
                            FinalDiagnosisIcd10Name = existing.FinalDiagnosisIcd10Name,
                            ChiefComplaint = existing.ChiefComplaint,
                            HistoryOfPresentIllness = existing.HistoryOfPresentIllness,
                            CourseInHospital = existing.CourseInHospital,
                            ProceduresPerformed = existing.ProceduresPerformed,
                            ConditionAtDischarge = existing.ConditionAtDischarge,
                            DischargeMedications = existing.DischargeMedications,
                            Medications = savedMedications,
                            FollowUpInstructions = existing.FollowUpInstructions,
                            FollowUpDate = existing.FollowUpDate,
                            DietInstructions = existing.DietInstructions,
                            ActivityRestrictions = existing.ActivityRestrictions,
                            AdditionalNotes = existing.AdditionalNotes,
                            KeyInvestigations = existing.KeyInvestigations,
                            WarningSigns = existing.WarningSigns,
                            PatientAdviceTranslationsJson = existing.PatientAdviceTranslationsJson,
                            SectionSourcesJson = existing.SectionSourcesJson,
                        },
                    };
                }

                // ── Fresh compose from source records ──────────────────────────────────
                var records = await DischargeStayRecords.LoadAsync(_context, request.HospitalId, request.AdmissionId, cancellationToken);
                if (records == null)
                    return new GetDischargeSummaryDraftResponseModel { Success = false, Message = "Admission not found." };

                var draft = new DischargeSummaryDraftModel
                {
                    // Admission.Diagnosis is the provisional diagnosis and AdmissionReason the
                    // complaint/reason (as captured on the Admit Patient form) — these were swapped.
                    AdmittingDiagnosis = admission.Diagnosis,
                    ChiefComplaint = admission.AdmissionReason,
                    CourseInHospital = DischargeSummaryComposer.ComposeCourseInHospital(records.RoundNotes),
                    ProceduresPerformed = DischargeSummaryComposer.ComposeProceduresPerformed(records.ProcedureLines),
                    KeyInvestigations = DischargeSummaryComposer.ComposeKeyInvestigations(records.Labs),
                    DischargeMedications = DischargeSummaryComposer.ComposeDischargeMedications(records.ActiveMedicationLines),
                    Medications = DischargeSummaryComposer.ComposeDischargeMedicationRows(records.ActiveMedicationLines),
                };
                draft.SectionSourcesJson = DischargeSummaryComposer.ComposeRecordSources(draft);

                return new GetDischargeSummaryDraftResponseModel { Success = true, Draft = draft };
            }
            catch (Exception)
            {
                return new GetDischargeSummaryDraftResponseModel { Success = false, Message = "Error composing the discharge summary draft." };
            }
        }
    }
}
