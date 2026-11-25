using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using Microsoft.AspNetCore.Http;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // AI-assisted discharge summary. Every request takes IDs, not clinical text pulled from the
    // client — the server re-reads the stay's records itself (same as GenerateDischargeNarrative).
    // Nothing here saves anything: results come back for the doctor to accept section by section.

    /// <summary>Draft the narrative sections of the discharge summary from the stay's records.</summary>
    [ExcludeFromCodeCoverage]
    public class DraftDischargeSectionsRequestModel : IRequest<DraftDischargeSectionsResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid AdmissionId { get; set; }
        /// <summary>Section keys to draft (see DischargeAiRules.DraftableSections); empty = all.</summary>
        public List<string>? Sections { get; set; }
        [JsonIgnore]
        public Guid CallerUserId { get; set; }
    }

    /// <summary>A recorded dictation, transcribed and sorted into discharge sections.</summary>
    [ExcludeFromCodeCoverage]
    public class DictateDischargeRequestModel : IRequest<DictateDischargeResponseModel>
    {
        public IFormFile? Audio { get; set; }
        public Guid HospitalId { get; set; }
        public Guid AdmissionId { get; set; }
        public string? Language { get; set; }   // optional hint "en"/"hi"; null = auto-detect (mixed speech)
        [JsonIgnore]
        public Guid CallerUserId { get; set; }
    }

    /// <summary>Rewrite one section's current text: SHORTER, DETAIL or PATIENT (plain words).</summary>
    [ExcludeFromCodeCoverage]
    public class RewriteDischargeSectionRequestModel : IRequest<RewriteDischargeSectionResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid AdmissionId { get; set; }
        public string? Section { get; set; }
        public string? Style { get; set; }
        public string? Text { get; set; }
        [JsonIgnore]
        public Guid CallerUserId { get; set; }
    }

    /// <summary>Translate the patient-advice sections into the hospital's second language.</summary>
    [ExcludeFromCodeCoverage]
    public class TranslateDischargeAdviceRequestModel : IRequest<TranslateDischargeAdviceResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid AdmissionId { get; set; }
        public string? Language { get; set; }   // e.g. "hi"
        /// <summary>English text per section key (followUpInstructions, dietInstructions, activityRestrictions, warningSigns).</summary>
        public Dictionary<string, string>? Sections { get; set; }
        [JsonIgnore]
        public Guid CallerUserId { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GetHospitalDischargePreferencesRequestModel : IRequest<HospitalDischargePreferencesResponseModel>
    {
        public Guid HospitalId { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class UpdateHospitalDischargePreferencesRequestModel : IRequest<HospitalDischargePreferencesResponseModel>
    {
        public Guid HospitalId { get; set; }
        /// <summary>null/"" = English only; "hi" = also print Hindi.</summary>
        public string? PatientLanguage { get; set; }
        public bool AiAssistEnabled { get; set; } = true;
        [JsonIgnore]
        public Guid CallerUserId { get; set; }
    }
}
