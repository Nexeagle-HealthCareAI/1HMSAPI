using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>
    /// AI-assisted discharge summary: draft sections from the stay's records, sort a dictation into
    /// sections, rewrite one section, and translate the patient advice. Review-and-apply only —
    /// none of these handlers writes to the discharge summary; the doctor accepts each result.
    /// Text sent to the AI provider is de-identified first (DischargeStayRecords.Redact).
    /// </summary>
    public class DischargeAiHandlers :
        IRequestHandler<DraftDischargeSectionsRequestModel, DraftDischargeSectionsResponseModel>,
        IRequestHandler<DictateDischargeRequestModel, DictateDischargeResponseModel>,
        IRequestHandler<RewriteDischargeSectionRequestModel, RewriteDischargeSectionResponseModel>,
        IRequestHandler<TranslateDischargeAdviceRequestModel, TranslateDischargeAdviceResponseModel>
    {
        public const string AiOffMessage = "AI help is switched off for this hospital. An admin can turn it on in Discharge settings.";
        public const string SignedMessage = "This discharge summary is signed. Use \"Edit again\" before changing it.";

        private readonly AppDbContext _context;
        private readonly IClinicalAiClient _ai;
        private readonly IVoiceRxService _voice;
        private readonly ITranslationService _translation;
        private readonly ILogger<DischargeAiHandlers> _logger;

        public DischargeAiHandlers(AppDbContext context, IClinicalAiClient ai, IVoiceRxService voice, ITranslationService translation, ILogger<DischargeAiHandlers> logger)
        {
            _context = context;
            _ai = ai;
            _voice = voice;
            _translation = translation;
            _logger = logger;
        }

        public async Task<DraftDischargeSectionsResponseModel> Handle(DraftDischargeSectionsRequestModel request, CancellationToken cancellationToken)
        {
            if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                return new() { Success = false, Message = "HospitalId and AdmissionId are required." };

            var gate = await CheckAsync(request.HospitalId, request.AdmissionId, cancellationToken);
            if (gate != null) return new() { Success = false, Message = gate };

            var sections = DischargeAiRules.ResolveSections(request.Sections);
            if (sections.Count == 0) return new() { Success = false, Message = "None of the requested sections can be drafted by AI." };

            var records = await DischargeStayRecords.LoadAsync(_context, request.HospitalId, request.AdmissionId, cancellationToken);
            if (records == null) return new() { Success = false, Message = "Admission not found." };
            if (!records.HasAnyRecords)
                return new() { Success = true, Message = "There are no records for this stay yet to draft from.", SourceSummary = records.Summary() };

            try
            {
                var json = await _ai.CompleteJsonAsync(DischargeAiRules.BuildDraftSystemPrompt(sections), records.AiSourceMaterial(), cancellationToken);
                var drafted = DischargeAiRules.ParseSections(json, sections);
                return new()
                {
                    Success = true,
                    Message = drafted.Count == 0 ? "AI could not draft anything from these records." : $"Drafted {drafted.Count} section(s).",
                    Sections = drafted,
                    SourceSummary = records.Summary(),
                };
            }
            catch (InvalidOperationException ex)
            {
                return new() { Success = false, Message = ex.Message };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Discharge AI draft failed for admission {AdmissionId}", request.AdmissionId);
                return new() { Success = false, Message = "Could not draft the summary just now. Please try again." };
            }
        }

        public async Task<DictateDischargeResponseModel> Handle(DictateDischargeRequestModel request, CancellationToken cancellationToken)
        {
            if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                return new() { Success = false, Message = "HospitalId and AdmissionId are required." };
            if (request.Audio == null || request.Audio.Length == 0)
                return new() { Success = false, Message = "No recording was received." };
            if (request.Audio.Length > DischargeAiRules.MaxAudioBytes)
                return new() { Success = false, Message = "The recording is too long. Please dictate in parts of a few minutes each." };

            var gate = await CheckAsync(request.HospitalId, request.AdmissionId, cancellationToken);
            if (gate != null) return new() { Success = false, Message = gate };

            var records = await DischargeStayRecords.LoadAsync(_context, request.HospitalId, request.AdmissionId, cancellationToken);
            if (records == null) return new() { Success = false, Message = "Admission not found." };

            try
            {
                byte[] audio;
                using (var ms = new MemoryStream())
                {
                    await request.Audio.CopyToAsync(ms, cancellationToken);
                    audio = ms.ToArray();
                }
                var language = request.Language is "en" or "hi" ? request.Language : null;
                var transcript = (await _voice.TranscribeAsync(audio, request.Audio.FileName, language, false, cancellationToken)).Trim();
                if (transcript.Length == 0)
                    return new() { Success = true, Message = "Nothing was heard in the recording.", Transcript = string.Empty };

                var currentMeds = await CurrentMedicineNamesAsync(records, cancellationToken);
                var context = "Current medicine list: " + (currentMeds.Count == 0 ? "none" : string.Join("; ", currentMeds)) +
                              "\n\nDictation:\n" + records.Redact(transcript);
                var json = await _ai.CompleteJsonAsync(DischargeAiRules.BuildDictationSystemPrompt(), context, cancellationToken);

                var changes = DischargeAiRules.ParseMedicationChanges(json)
                    .Select(c => new DischargeMedicationSuggestionModel
                    {
                        MedicineName = c.MedicineName,
                        Action = c.Action,
                        NewDose = c.NewDose,
                        Note = c.Note,
                        MatchesMedicine = DischargeAiRules.MatchMedicine(c.MedicineName, currentMeds),
                    })
                    // A change/stop for a medicine that isn't on the list can't be applied — drop it
                    // rather than guess; "ADD" is the only action that names a new medicine.
                    .Where(c => c.Action == "ADD" || c.MatchesMedicine != null)
                    .ToList();

                return new()
                {
                    Success = true,
                    Message = "Dictation sorted into sections — review each one before accepting.",
                    Transcript = transcript,
                    Sections = DischargeAiRules.ParseSections(json, DischargeAiRules.DraftableSections.Keys),
                    MedicationChanges = changes,
                    FollowUpAfterDays = DischargeAiRules.ParseFollowUpDays(json),
                };
            }
            catch (InvalidOperationException ex)
            {
                return new() { Success = false, Message = ex.Message };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Discharge dictation failed for admission {AdmissionId}", request.AdmissionId);
                return new() { Success = false, Message = "Could not process the dictation just now. Please try again." };
            }
        }

        public async Task<RewriteDischargeSectionResponseModel> Handle(RewriteDischargeSectionRequestModel request, CancellationToken cancellationToken)
        {
            if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                return new() { Success = false, Message = "HospitalId and AdmissionId are required." };
            var section = DischargeAiRules.ResolveSections(new[] { request.Section ?? string.Empty }).FirstOrDefault();
            if (section == null || string.IsNullOrWhiteSpace(request.Section))
                return new() { Success = false, Message = "This section can't be rewritten by AI." };
            var style = DischargeAiRules.NormalizeStyle(request.Style);
            if (style == null) return new() { Success = false, Message = "Choose shorter, more detail or patient-friendly." };
            if (string.IsNullOrWhiteSpace(request.Text)) return new() { Success = false, Message = "There is no text to rewrite." };
            if (request.Text.Length > DischargeAiRules.MaxSectionLength * 2) return new() { Success = false, Message = "The text is too long to rewrite in one go." };

            var gate = await CheckAsync(request.HospitalId, request.AdmissionId, cancellationToken);
            if (gate != null) return new() { Success = false, Message = gate };

            var records = await DischargeStayRecords.LoadAsync(_context, request.HospitalId, request.AdmissionId, cancellationToken);
            if (records == null) return new() { Success = false, Message = "Admission not found." };

            try
            {
                var text = await _ai.CompleteTextAsync(DischargeAiRules.BuildRewriteSystemPrompt(section, style), records.Redact(request.Text), cancellationToken);
                var clean = DischargeAiRules.CleanText(text);
                return clean.Length == 0
                    ? new() { Success = false, Message = "AI returned nothing. Please try again." }
                    : new() { Success = true, Text = clean };
            }
            catch (InvalidOperationException ex)
            {
                return new() { Success = false, Message = ex.Message };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Discharge rewrite failed for admission {AdmissionId}", request.AdmissionId);
                return new() { Success = false, Message = "Could not rewrite just now. Please try again." };
            }
        }

        public async Task<TranslateDischargeAdviceResponseModel> Handle(TranslateDischargeAdviceRequestModel request, CancellationToken cancellationToken)
        {
            if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                return new() { Success = false, Message = "HospitalId and AdmissionId are required." };
            var language = DischargeAiRules.NormalizeLanguage(request.Language);
            if (language == null) return new() { Success = false, Message = "That language isn't available for the printout." };

            var texts = (request.Sections ?? new())
                .Where(kv => DischargeAiRules.PatientAdviceSections.Contains(kv.Key) && !string.IsNullOrWhiteSpace(kv.Value))
                .ToDictionary(kv => kv.Key, kv => kv.Value.Length > DischargeAiRules.MaxSectionLength ? kv.Value[..DischargeAiRules.MaxSectionLength] : kv.Value);
            if (texts.Count == 0) return new() { Success = true, Language = language, Message = "There is no patient advice to translate yet." };

            var gate = await CheckAsync(request.HospitalId, request.AdmissionId, cancellationToken);
            if (gate != null) return new() { Success = false, Message = gate };

            var records = await DischargeStayRecords.LoadAsync(_context, request.HospitalId, request.AdmissionId, cancellationToken);
            if (records == null) return new() { Success = false, Message = "Admission not found." };

            try
            {
                var redacted = texts.ToDictionary(kv => kv.Key, kv => records.Redact(kv.Value));
                var translated = await _translation.TranslateMultipleAsync(redacted, DischargeAiRules.PatientLanguages[language]);
                var result = translated
                    .Where(kv => texts.ContainsKey(kv.Key) && !string.IsNullOrWhiteSpace(kv.Value)
                                 && !kv.Value.StartsWith("(Mock translation", StringComparison.Ordinal))
                    .ToDictionary(kv => kv.Key, kv => DischargeAiRules.CleanText(kv.Value));
                return result.Count == 0
                    ? new() { Success = false, Language = language, Message = "Translation isn't available right now. Please try again." }
                    : new() { Success = true, Language = language, Sections = result };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Discharge advice translation failed for admission {AdmissionId}", request.AdmissionId);
                return new() { Success = false, Language = language, Message = "Could not translate just now. Please try again." };
            }
        }

        /// <summary>Common gate: AI configured on the server, switched on for the hospital, summary not signed.</summary>
        private async Task<string?> CheckAsync(Guid hospitalId, Guid admissionId, CancellationToken cancellationToken)
        {
            if (!_ai.IsConfigured) return _ai.NotConfiguredMessage;
            var prefs = await HospitalDischargeDefaults.FindAsync(_context, hospitalId, cancellationToken);
            if (prefs?.AiAssistEnabled == false) return AiOffMessage;
            var signed = await _context.DischargeSummary
                .AnyAsync(d => d.HospitalId == hospitalId && d.AdmissionId == admissionId && d.IsSigned, cancellationToken);
            return signed ? SignedMessage : null;
        }

        /// <summary>The discharge medicine list as it stands: the saved list once a summary exists, else the active orders.</summary>
        private async Task<List<string>> CurrentMedicineNamesAsync(DischargeStayRecords records, CancellationToken cancellationToken)
        {
            var summaryId = await _context.DischargeSummary
                .Where(d => d.HospitalId == records.Admission.HospitalId && d.AdmissionId == records.Admission.AdmissionId)
                .Select(d => (Guid?)d.DischargeSummaryId)
                .FirstOrDefaultAsync(cancellationToken);
            if (summaryId.HasValue)
            {
                var saved = await _context.DischargeMedication
                    .Where(m => m.DischargeSummaryId == summaryId.Value)
                    .OrderBy(m => m.DisplayOrder)
                    .Select(m => m.MedicineName)
                    .ToListAsync(cancellationToken);
                var names = saved.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n!).ToList();
                if (names.Count > 0) return names;
            }
            return records.ActiveMedicationLines.Select(l => l.ItemName).Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n!).Distinct().ToList();
        }
    }

    /// <summary>The hospital's own discharge-settings row (DoctorId = Guid.Empty): letterhead every
    /// doctor falls back to, second print language, and the AI switch.</summary>
    public static class HospitalDischargeDefaults
    {
        public static Task<DischargeSetting?> FindAsync(AppDbContext context, Guid hospitalId, CancellationToken cancellationToken) =>
            context.DischargeSettings.FirstOrDefaultAsync(s => s.HospitalId == hospitalId && s.DoctorId == Guid.Empty, cancellationToken);
    }

    public class HospitalDischargePreferencesHandlers :
        IRequestHandler<GetHospitalDischargePreferencesRequestModel, HospitalDischargePreferencesResponseModel>,
        IRequestHandler<UpdateHospitalDischargePreferencesRequestModel, HospitalDischargePreferencesResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IClinicalAiClient _ai;

        public HospitalDischargePreferencesHandlers(AppDbContext context, IClinicalAiClient ai)
        {
            _context = context;
            _ai = ai;
        }

        public async Task<HospitalDischargePreferencesResponseModel> Handle(GetHospitalDischargePreferencesRequestModel request, CancellationToken cancellationToken)
        {
            if (request.HospitalId == Guid.Empty) return new() { Success = false, Message = "HospitalId is required." };
            var row = await HospitalDischargeDefaults.FindAsync(_context, request.HospitalId, cancellationToken);
            return ToResponse(row, "OK");
        }

        public async Task<HospitalDischargePreferencesResponseModel> Handle(UpdateHospitalDischargePreferencesRequestModel request, CancellationToken cancellationToken)
        {
            if (request.HospitalId == Guid.Empty) return new() { Success = false, Message = "HospitalId is required." };
            string? language = null;
            if (!string.IsNullOrWhiteSpace(request.PatientLanguage))
            {
                language = DischargeAiRules.NormalizeLanguage(request.PatientLanguage);
                if (language == null) return new() { Success = false, Message = "That language isn't available for the printout." };
            }

            var row = await HospitalDischargeDefaults.FindAsync(_context, request.HospitalId, cancellationToken);
            var now = DateTime.UtcNow;
            if (row == null)
            {
                row = new DischargeSetting
                {
                    DischargeSettingId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    DoctorId = Guid.Empty,
                    CreatedByUserId = request.CallerUserId == Guid.Empty ? null : request.CallerUserId,
                    CreatedAt = now,
                };
                _context.DischargeSettings.Add(row);
            }
            row.PatientLanguage = language;
            row.AiAssistEnabled = request.AiAssistEnabled;
            row.UpdatedAt = now;
            await _context.SaveChangesAsync(cancellationToken);
            return ToResponse(row, "Discharge settings saved.");
        }

        private HospitalDischargePreferencesResponseModel ToResponse(DischargeSetting? row, string message) => new()
        {
            Success = true,
            Message = message,
            PatientLanguage = DischargeAiRules.NormalizeLanguage(row?.PatientLanguage),
            AiAssistEnabled = row?.AiAssistEnabled != false,
            AiAvailable = _ai.IsConfigured,
            HasHospitalLetterhead = !string.IsNullOrWhiteSpace(row?.URI),
            LanguageOptions = DischargeAiRules.PatientLanguages.Select(l => new PatientLanguageOption { Code = l.Key, Name = l.Value }).ToList(),
        };
    }
}
