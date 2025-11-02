using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetDepartmentsResponseModel
    {
        public List<DepartmentInfo> Departments { get; set; } = new List<DepartmentInfo>();
    }

    [ExcludeFromCodeCoverage]
    public class DepartmentInfo
    {
        public Guid DepartmentID { get; set; }
        public Guid? HospitalID { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public bool IsActive { get; set; }
    }
}
