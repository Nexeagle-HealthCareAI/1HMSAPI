using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetPublicDoctorsResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<PublicDoctorInfo> Doctors { get; set; } = new();
    }

    // Public-safe field set only — no LicenseNumber, MedicalCouncil, RegistrationYear, UserId,
    // mobile/email, or anything queue/schedule-internal.
    [ExcludeFromCodeCoverage]
    public class PublicDoctorInfo
    {
        public Guid DoctorId { get; set; }
        public string? FullName { get; set; }
        public string? PhotoUrl { get; set; }
        public string? Qualification { get; set; }
        public int? ExperienceYears { get; set; }
        public string? Bio { get; set; }
        public string? DepartmentName { get; set; }
        public List<string> Specializations { get; set; } = new();
        public List<string> Languages { get; set; } = new();
        // Computed from non-hidden DoctorReviews — null/0 when the doctor has no reviews yet, so
        // the frontend's existing "hide the badge when falsy" rendering is unaffected.
        public double? Rating { get; set; }
        public int ReviewCount { get; set; }
        // OPD_CONSULT DoctorFees.Amount at this doctor's (canonical) hospital — null when no
        // active fee is configured, so the frontend falls back to "Accepting patients".
        public decimal? Fee { get; set; }

        // Which (publicly-listed) hospital this doctor belongs to — needed now that the
        // directory spans every opted-in hospital, not just one scoped by an API key.
        public Guid HospitalId { get; set; }
        public string? HospitalName { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        // GPS pin for a "get directions" link — inherited from the hospital, since a doctor
        // doesn't have their own address (see Hospital.Latitude/Longitude).
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
    }
}
