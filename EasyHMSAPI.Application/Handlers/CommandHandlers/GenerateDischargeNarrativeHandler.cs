using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.Services.Interfaces;
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

            // Same records (and de-identification) as the full AI draft.
            var records = await DischargeStayRecords.LoadAsync(_context, request.HospitalId, request.AdmissionId, cancellationToken);
            if (records == null)
                return Fail("Admission not found.");

            var prefs = await HospitalDischargeDefaults.FindAsync(_context, request.HospitalId, cancellationToken);
            if (prefs?.AiAssistEnabled == false)
                return Fail(DischargeAiHandlers.AiOffMessage);

            if (!records.HasAnyRecords)
                return new GenerateDischargeNarrativeResponseModel { Success = true, Message = "No clinical records to summarize yet.", CourseInHospital = string.Empty };
            var sourceMaterial = records.AiSourceMaterial();

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
