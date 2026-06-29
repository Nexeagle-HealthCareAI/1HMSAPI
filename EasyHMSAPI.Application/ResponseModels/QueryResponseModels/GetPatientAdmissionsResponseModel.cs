using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetPatientAdmissionsResponseModel
    {
        public bool? Success { get; set; }
        public string? Message { get; set; }
        public AdmissionPatientDetail? Patient { get; set; }
        public List<AdmissionHistoryItem> Admissions { get; set; } = new();
    }

    /// <summary>Full demographics used to pre-fill the admission form for a returning patient.</summary>
    [ExcludeFromCodeCoverage]
    public class AdmissionPatientDetail
    {
        public string PatientId { get; set; } = null!;
        public string? FullName { get; set; }
        public string? Mobile { get; set; }
        public short? Age { get; set; }
        public string? AgeUnit { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? Sex { get; set; }
        public string? BloodGroup { get; set; }
        public string? Religion { get; set; }
        public string? Nationality { get; set; }

        public string? FlatHouse { get; set; }
        public string? Street { get; set; }
        public string? AddressLine { get; set; }
        public string? Block { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
        public string? State { get; set; }
        public string? Pincode { get; set; }
        public string? Country { get; set; }

        public string? AlternateMobile { get; set; }
        public string? Email { get; set; }
        public string? EmergencyContactName { get; set; }
        public string? EmergencyContactRelation { get; set; }
        public string? EmergencyContactPhone { get; set; }

        // Aadhaar is returned masked (only last 4 digits visible); PAN/ABHA as stored.
        public string? AadhaarMasked { get; set; }
        public string? PanNumber { get; set; }
        public string? AbhaId { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class AdmissionHistoryItem
    {
        public Guid AdmissionId { get; set; }
        public string AdmissionNo { get; set; } = null!;
        public string? AdmissionType { get; set; }
        public DateTime AdmittedAt { get; set; }
        public DateTime? DischargedAt { get; set; }
        public string StatusCode { get; set; } = null!;
        public string? AdmissionReason { get; set; }
        public string? Diagnosis { get; set; }
        // Short discharge-summary preview (truncated server-side).
        public string? DischargeNotesPreview { get; set; }
    }
}
