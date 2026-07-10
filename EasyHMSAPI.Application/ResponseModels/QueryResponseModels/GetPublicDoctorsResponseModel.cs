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
    }
}
