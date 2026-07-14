using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetDischargeSummaryDraftResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public DischargeSummaryDraftModel? Draft { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class DischargeSummaryDraftModel
    {
        // Set only when a row already exists (doctor re-opening a saved draft, or viewing a
        // signed one) — null for a freshly-composed, never-saved draft.
        public Guid? DischargeSummaryId { get; set; }
        public bool IsSigned { get; set; }
        public DateTime? SignedAt { get; set; }
        public string? SignedByDoctorName { get; set; }

        public string? AdmittingDiagnosis { get; set; }
        public string? FinalDiagnosis { get; set; }
        public string? FinalDiagnosisIcd10Code { get; set; }
        public string? FinalDiagnosisIcd10Name { get; set; }
        public string? ChiefComplaint { get; set; }
        public string? HistoryOfPresentIllness { get; set; }
        public string? CourseInHospital { get; set; }
        public string? ProceduresPerformed { get; set; }
        public string? ConditionAtDischarge { get; set; }
        public string? DischargeMedications { get; set; }
        public List<DischargeMedicationModel> Medications { get; set; } = new();
        public string? FollowUpInstructions { get; set; }
        public DateTime? FollowUpDate { get; set; }
        public string? DietInstructions { get; set; }
        public string? ActivityRestrictions { get; set; }
        public string? AdditionalNotes { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class DischargeMedicationModel
    {
        public string? MedicineName { get; set; }
        public string? Dosage { get; set; }
        public string? Route { get; set; }
        public string? Frequency { get; set; }
        public string? Durations { get; set; }
        public string? Instructions { get; set; }
        public string? SaltName { get; set; }
        public int? DisplayOrder { get; set; }
    }
}
