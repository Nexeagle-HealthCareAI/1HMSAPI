using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    /// <summary>
    /// Result of parsing a doctor's voice dictation into structured prescription fields. The doctor
    /// reviews/edits this in a panel before it populates the pad — it is never applied automatically.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class ParseVoiceRxResponseModel
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Transcript { get; set; } = string.Empty;
        public VoiceRxFieldsModel Fields { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class VoiceRxFieldsModel
    {
        public string? ChiefComplaint { get; set; }
        public string? History { get; set; }
        public string? Examination { get; set; }          // general examination
        public string? SystemicExamination { get; set; }
        public string? Diagnosis { get; set; }
        public List<string>? Investigations { get; set; }
        public List<string>? Procedures { get; set; }
        public List<VoiceRxMedicationModel>? Medications { get; set; }
        public List<VoiceRxAdviceModel>? Advice { get; set; }
        public VoiceRxFollowUpModel? FollowUp { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class VoiceRxMedicationModel
    {
        public string? Name { get; set; }
        public string? Dose { get; set; }
        public string? Frequency { get; set; }
        public string? Duration { get; set; }
        public string? Instructions { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class VoiceRxAdviceModel
    {
        public string? Advice { get; set; }
        public string? Duration { get; set; }
        public string? Notes { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class VoiceRxFollowUpModel
    {
        public string? FollowUpOn { get; set; }   // free text / relative ("after 5 days") or ISO date
        public string? Reason { get; set; }
    }
}
