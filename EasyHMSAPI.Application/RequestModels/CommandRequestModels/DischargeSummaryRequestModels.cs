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
        public string? ChiefComplaint { get; set; }
        public string? HistoryOfPresentIllness { get; set; }
        public string? CourseInHospital { get; set; }
        public string? ProceduresPerformed { get; set; }
        public string? ConditionAtDischarge { get; set; }
        public string? DischargeMedications { get; set; }
        public string? FollowUpInstructions { get; set; }
        public DateTime? FollowUpDate { get; set; }
        public string? DietInstructions { get; set; }
        public string? ActivityRestrictions { get; set; }
        public string? AdditionalNotes { get; set; }
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
