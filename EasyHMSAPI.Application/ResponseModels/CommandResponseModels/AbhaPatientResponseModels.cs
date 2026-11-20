using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    /// <summary>A patient of this hospital, as the ABHA registration step shows it beside the ABHA.</summary>
    [ExcludeFromCodeCoverage]
    public class AbhaPatientSummary
    {
        public string PatientId { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public string? Mobile { get; set; }
        public string? Email { get; set; }
        public string? Sex { get; set; }
        public short? Age { get; set; }
        public string? AgeUnit { get; set; }
        public string? DateOfBirth { get; set; }
        public string? AddressLine { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
        public string? State { get; set; }
        public string? Pincode { get; set; }
        public string? AbhaId { get; set; }
        public DateTime? RegisteredAt { get; set; }
        public List<string> MatchedOn { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class MatchAbhaPatientResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        /// <summary>The patient this ABHA is already linked to (VRFY_ABHA_308), if any.</summary>
        public AbhaPatientSummary? AlreadyLinked { get; set; }
        /// <summary>Patients who may be this person (CRT_ABHA_116), best first.</summary>
        public List<AbhaPatientSummary> Candidates { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class RegisterAbhaPatientResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? PatientId { get; set; }
        /// <summary>True when a new patient record was made; false when an existing one was linked or refreshed.</summary>
        public bool Created { get; set; }
        public Guid? AbhaAccountId { get; set; }
        /// <summary>Set when the ABHA is already on another patient: nothing was saved, no duplicate made.</summary>
        public string? AlreadyLinkedPatientId { get; set; }
    }
}
