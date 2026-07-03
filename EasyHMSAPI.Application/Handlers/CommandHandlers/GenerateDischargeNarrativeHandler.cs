using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>
    /// AI-assist for the discharge summary's "Course in Hospital" narrative — reuses the exact
    /// same source-material assembly as GetDischargeSummaryDraftHandler (via
    /// DischargeSummaryComposer), so the AI never sees anything the deterministic draft doesn't
    /// already show the doctor. Review-and-apply, nothing is saved here (same contract as Voice Rx).
    /// </summary>
    public class GenerateDischargeNarrativeHandler : IRequestHandler<GenerateDischargeNarrativeRequestModel, GenerateDischargeNarrativeResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IVoiceRxService _voiceRx;

        public GenerateDischargeNarrativeHandler(AppDbContext context, IVoiceRxService voiceRx)
        {
            _context = context;
            _voiceRx = voiceRx;
        }

        public async Task<GenerateDischargeNarrativeResponseModel> Handle(GenerateDischargeNarrativeRequestModel request, CancellationToken cancellationToken)
        {
            if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                return Fail("HospitalId and AdmissionId are required.");

            var admission = await _context.Admission
                .FirstOrDefaultAsync(a => a.AdmissionId == request.AdmissionId && a.HospitalId == request.HospitalId, cancellationToken);
            if (admission == null)
                return Fail("Admission not found.");

            var roundNotes = await _context.RoundNote
                .Where(r => r.HospitalId == request.HospitalId && r.AdmissionId == request.AdmissionId)
                .OrderBy(r => r.NotedAt)
                .ToListAsync(cancellationToken);

            var procedureOrderIds = await _context.ClinicalOrder
                .Where(o => o.HospitalId == request.HospitalId && o.AdmissionId == request.AdmissionId
                    && o.OrderType == IpdConstants.ClinicalOrderType.Procedure)
                .Select(o => o.OrderId)
                .ToListAsync(cancellationToken);
            var procedureLines = procedureOrderIds.Count == 0
                ? new List<Domain.Entities.ClinicalOrderLine>()
                : await _context.ClinicalOrderLine
                    .Where(l => procedureOrderIds.Contains(l.OrderId))
                    .OrderBy(l => l.ScheduledAt ?? l.CreatedAt)
                    .ToListAsync(cancellationToken);

            var medicationOrderIds = await _context.ClinicalOrder
                .Where(o => o.HospitalId == request.HospitalId && o.AdmissionId == request.AdmissionId
                    && o.OrderType == IpdConstants.ClinicalOrderType.Medication)
                .Select(o => o.OrderId)
                .ToListAsync(cancellationToken);
            var medicationLines = medicationOrderIds.Count == 0
                ? new List<Domain.Entities.ClinicalOrderLine>()
                : await _context.ClinicalOrderLine
                    .Where(l => medicationOrderIds.Contains(l.OrderId) && l.StatusCode == IpdConstants.ClinicalOrderLineStatus.Active)
                    .ToListAsync(cancellationToken);

            var sourceMaterial = DischargeSummaryComposer.ComposeNarrativeSourceMaterial(
                admission.AdmissionReason, admission.Diagnosis, roundNotes, procedureLines, medicationLines);

            if (string.IsNullOrWhiteSpace(sourceMaterial))
                return new GenerateDischargeNarrativeResponseModel { Success = true, Message = "No clinical records to summarize yet.", CourseInHospital = string.Empty };

            try
            {
                var narrative = await _voiceRx.NarrateAsync(sourceMaterial, "Course in Hospital", cancellationToken);
                return new GenerateDischargeNarrativeResponseModel { Success = true, Message = "Narrative generated.", CourseInHospital = narrative };
            }
            catch (InvalidOperationException ex)
            {
                return Fail(ex.Message);
            }
            catch
            {
                return Fail("Could not generate the narrative. Please try again.");
            }
        }

        private static GenerateDischargeNarrativeResponseModel Fail(string message) => new() { Success = false, Message = message };
    }
}
