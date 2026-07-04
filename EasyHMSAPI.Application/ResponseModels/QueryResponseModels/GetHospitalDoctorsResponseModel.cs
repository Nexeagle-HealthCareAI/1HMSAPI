using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetHospitalDoctorsResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<HospitalDoctorItem> Doctors { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class HospitalDoctorItem
    {
        public Guid DoctorId { get; set; }
        public string? FullName { get; set; }
        public string? DepartmentName { get; set; }
    }
}
