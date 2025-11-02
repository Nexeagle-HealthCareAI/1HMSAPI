using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetDepartmentDoctorsResponseModel
    {
        public List<DepartmentDoctorInfo> Doctors { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class DepartmentDoctorInfo
    {
        public Guid DoctorId { get; set; }
        public string? DoctorName { get; set; }
        public string? LicenseNumber { get; set; }
        public List<string>? Qualifications { get; set; }
        public List<string>? Specializations { get; set; }
    }
}
