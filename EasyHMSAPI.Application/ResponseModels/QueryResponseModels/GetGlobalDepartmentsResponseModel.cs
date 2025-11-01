namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    public class GetGlobalDepartmentsResponseModel
    {
        public List<DepartmentInfo> Departments { get; set; } = new List<DepartmentInfo>();
    }
}
