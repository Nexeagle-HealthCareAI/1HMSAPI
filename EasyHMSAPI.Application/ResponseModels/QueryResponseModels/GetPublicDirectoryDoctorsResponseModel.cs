using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetPublicDirectoryDoctorsResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<PublicDirectoryDoctorItem> Doctors { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class PublicDirectoryDoctorItem
    {
        public Guid DoctorId { get; set; }
        public Guid UserId { get; set; }
        public string? FullName { get; set; }
        public string? PhotoUrl { get; set; }
        public Guid? DepartmentId { get; set; }
        public string? DepartmentName { get; set; }
        // Required by PUT doctors/profile's existing HospitalDepartmentMappingId guard — the tile
        // editor's "Save" round-trips this back unchanged when it isn't editing department.
        public Guid? HospitalDepartmentMappingId { get; set; }
        public string? LicenseNumber { get; set; }
        public string? Qualification { get; set; }
        public int? ExperienceYears { get; set; }
        public string? Bio { get; set; }
        public List<string> Specializations { get; set; } = new();
        public List<string> Languages { get; set; } = new();
        public string? PublicContactEmail { get; set; }
        public string? PublicContactPhone { get; set; }
        // Whether this doctor currently shows on the platform-wide public directory (also requires
        // the hospital itself to be publicly listed — see Hospital.IsPubliclyListed).
        public bool IsPubliclyListed { get; set; }
    }
}
