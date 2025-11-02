using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class DoctorGetResponseModel
    {
        public Guid DoctorId { get; set; }
        public Guid UserId { get; set; }
        public string LicenseNumber { get; set; } = null!;
        public List<string> Qualifications { get; set; } = new List<string>();
        public int? ExperienceYears { get; set; }
        public string? MedicalCouncil { get; set; }
        public int? RegistrationYear { get; set; }
        public string? Bio { get; set; }
        public Guid? PrimaryDepartmentID { get; set; }
        public string? PrimaryDepartmentName { get; set; }
        public int ProfileCompletionPercentage { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<DoctorDepartmentInfo> DoctorDepartments { get; set; } = new List<DoctorDepartmentInfo>();
        public List<DoctorSpecializationInfo> DoctorSpecializations { get; set; } = new List<DoctorSpecializationInfo>();
    }

    [ExcludeFromCodeCoverage]
    public class DoctorDepartmentInfo
    {
        public Guid DoctorDepartmentId { get; set; }
        public Guid DepartmentId { get; set; }
        public string DepartmentName { get; set; } = null!;
        public string? DepartmentDescription { get; set; }
        public DateTime AssignedAt { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class DoctorSpecializationInfo
    {
        public Guid DoctorSpecializationId { get; set; }
        public Guid SpecializationId { get; set; }
        public string SpecializationName { get; set; } = null!;
        public string? SpecializationDescription { get; set; }
        public DateTime AssignedAt { get; set; }
    }
}
