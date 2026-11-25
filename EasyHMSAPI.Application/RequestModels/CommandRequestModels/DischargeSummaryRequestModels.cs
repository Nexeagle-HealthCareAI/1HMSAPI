using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Upsert by (HospitalId, AdmissionId) — matches the table's own UNIQUE constraint, so this is
    // the only shape the schema allows anyway. Rejected once the row is signed.
    [ExcludeFromCodeCoverage]
    public class SaveDischargeSummaryRequestModel : IRequest<SaveDischargeSummaryResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        public Guid AdmissionId { get; set; }

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
        public List<DischargeMedicationRequestModel>? Medications { get; set; }
        public string? FollowUpInstructions { get; set; }
        public DateTime? FollowUpDate { get; set; }
        public string? DietInstructions { get; set; }
        public string? ActivityRestrictions { get; set; }
        public string? AdditionalNotes { get; set; }

        // Nov 2026. Left unchanged when the client doesn't send them (null), so an older client
        // can't wipe them; send "" to clear.
        public string? KeyInvestigations { get; set; }
        public string? WarningSigns { get; set; }
        /// <summary>{"hi":{"dietInstructions":"…",…}} — the reviewed second-language patient advice.</summary>
        public string? PatientAdviceTranslationsJson { get; set; }
        /// <summary>{"courseInHospital":"AI",…} — RECORDS / AI / DOCTOR per section, for audit.</summary>
        public string? SectionSourcesJson { get; set; }
    }

    // Structured discharge/home medication line item — field-for-field matching PrescriptionMedicine,
    // so the same "search a drug, capture dose/route/frequency/duration/instructions" UX applies here.
    [ExcludeFromCodeCoverage]
    public class DischargeMedicationRequestModel
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

    // Signing locks further edits — a medico-legal document handed to the patient / potentially
    // submitted to a TPA, unlike round notes' internal-only addendum model. No unsign/reopen this
    // phase.
    [ExcludeFromCodeCoverage]
    public class SignDischargeSummaryRequestModel : IRequest<SignDischargeSummaryResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }
        [JsonIgnore]
        public Guid? LoggedInUserId { get; set; }

        public Guid AdmissionId { get; set; }
        public string? DoctorName { get; set; }
    }
}
