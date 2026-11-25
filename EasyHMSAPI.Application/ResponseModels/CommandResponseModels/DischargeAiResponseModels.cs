using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class DraftDischargeSectionsResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        /// <summary>Drafted text per section key; sections the records had nothing for are left out.</summary>
        public Dictionary<string, string> Sections { get; set; } = new();
        /// <summary>What was used, e.g. "6 round notes, 2 procedures, 9 lab results".</summary>
        public string? SourceSummary { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class DischargeMedicationSuggestionModel
    {
        public string MedicineName { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;   // CONTINUE / CHANGE / STOP / ADD
        public string? NewDose { get; set; }
        public string? Note { get; set; }
        /// <summary>Name of the matching medicine already on the discharge list, if any.</summary>
        public string? MatchesMedicine { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class DictateDischargeResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? Transcript { get; set; }
        public Dictionary<string, string> Sections { get; set; } = new();
        public List<DischargeMedicationSuggestionModel> MedicationChanges { get; set; } = new();
        public int? FollowUpAfterDays { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class RewriteDischargeSectionResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? Text { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class TranslateDischargeAdviceResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? Language { get; set; }
        public Dictionary<string, string> Sections { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class HospitalDischargePreferencesResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? PatientLanguage { get; set; }
        public bool AiAssistEnabled { get; set; } = true;
        /// <summary>False when the server has no AI key — the screen then hides the AI buttons.</summary>
        public bool AiAvailable { get; set; }
        /// <summary>True when the hospital has its own letterhead uploaded (doctors without one use it).</summary>
        public bool HasHospitalLetterhead { get; set; }
        public List<PatientLanguageOption> LanguageOptions { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class PatientLanguageOption
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}
