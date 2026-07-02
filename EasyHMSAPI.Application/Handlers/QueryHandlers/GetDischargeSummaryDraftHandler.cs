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
                            ChiefComplaint = existing.ChiefComplaint,
                            HistoryOfPresentIllness = existing.HistoryOfPresentIllness,
                            CourseInHospital = existing.CourseInHospital,
                            ProceduresPerformed = existing.ProceduresPerformed,
                            ConditionAtDischarge = existing.ConditionAtDischarge,
                            DischargeMedications = existing.DischargeMedications,
                            FollowUpInstructions = existing.FollowUpInstructions,
                            FollowUpDate = existing.FollowUpDate,
                            DietInstructions = existing.DietInstructions,
                            ActivityRestrictions = existing.ActivityRestrictions,
                            AdditionalNotes = existing.AdditionalNotes,
                        },
                    };
                }

                // ── Fresh compose from source records ──────────────────────────────────
                var roundNotes = await _context.RoundNote
                    .Where(r => r.HospitalId == request.HospitalId && r.AdmissionId == request.AdmissionId)
                    .OrderBy(r => r.NotedAt)
                    .ToListAsync(cancellationToken);

                var courseInHospital = DischargeSummaryComposer.ComposeCourseInHospital(roundNotes);

                var procedureOrders = await _context.ClinicalOrder
                    .Where(o => o.HospitalId == request.HospitalId && o.AdmissionId == request.AdmissionId
                        && o.OrderType == IpdConstants.ClinicalOrderType.Procedure)
                    .ToListAsync(cancellationToken);
                var procedureOrderIds = procedureOrders.Select(o => o.OrderId).ToList();
                var procedureLines = procedureOrderIds.Count == 0
                    ? new List<Domain.Entities.ClinicalOrderLine>()
                    : await _context.ClinicalOrderLine
                        .Where(l => procedureOrderIds.Contains(l.OrderId))
                        .OrderBy(l => l.ScheduledAt ?? l.CreatedAt)
                        .ToListAsync(cancellationToken);
                var proceduresPerformed = DischargeSummaryComposer.ComposeProceduresPerformed(procedureLines);

                var medicationOrders = await _context.ClinicalOrder
                    .Where(o => o.HospitalId == request.HospitalId && o.AdmissionId == request.AdmissionId
                        && o.OrderType == IpdConstants.ClinicalOrderType.Medication)
                    .ToListAsync(cancellationToken);
                var medicationOrderIds = medicationOrders.Select(o => o.OrderId).ToList();
                var activeMedicationLines = medicationOrderIds.Count == 0
                    ? new List<Domain.Entities.ClinicalOrderLine>()
                    : await _context.ClinicalOrderLine
                        .Where(l => medicationOrderIds.Contains(l.OrderId) && l.StatusCode == IpdConstants.ClinicalOrderLineStatus.Active)
                        .ToListAsync(cancellationToken);
                var dischargeMedications = DischargeSummaryComposer.ComposeDischargeMedications(activeMedicationLines);

                return new GetDischargeSummaryDraftResponseModel
                {
                    Success = true,
                    Draft = new DischargeSummaryDraftModel
                    {
                        AdmittingDiagnosis = admission.AdmissionReason,
                        ChiefComplaint = admission.Diagnosis,
                        CourseInHospital = courseInHospital,
                        ProceduresPerformed = proceduresPerformed,
                        DischargeMedications = dischargeMedications,
                    },
                };
            }
            catch (Exception)
            {
                return new GetDischargeSummaryDraftResponseModel { Success = false, Message = "Error composing the discharge summary draft." };
            }
        }
    }
}
