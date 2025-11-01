namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    public class GetAppointmentDepartmentsResponseModel
    {
        public List<AppointmentDepartmentInfo> Departments { get; set; } = new();
    }

    public class AppointmentDepartmentInfo
    {
        public Guid DepartmentId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
    }
}
