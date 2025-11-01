namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    public class GetDepartmentsResponseModel
    {
        public List<DepartmentInfo> Departments { get; set; } = new List<DepartmentInfo>();
    }

    public class DepartmentInfo
    {
        public Guid DepartmentID { get; set; }
        public Guid? HospitalID { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public bool IsActive { get; set; }
    }
}
