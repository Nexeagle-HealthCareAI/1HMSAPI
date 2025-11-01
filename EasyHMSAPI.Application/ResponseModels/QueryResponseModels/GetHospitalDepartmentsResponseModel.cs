namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    public class GetHospitalDepartmentsResponseModel
    {
        public List<HospitalDepartmentInfo> Departments { get; set; } = new List<HospitalDepartmentInfo>();
    }

    public class HospitalDepartmentInfo
    {
        public Guid MappingID { get; set; }
        public Guid HospitalID { get; set; }
        public Guid DepartmentID { get; set; }
        public string DepartmentName { get; set; } = null!;
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public DateTime MappedAt { get; set; }
    }
}
