using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetGlobalDepartmentsResponseModel
    {
        public List<DepartmentInfo> Departments { get; set; } = new List<DepartmentInfo>();
    }
}
