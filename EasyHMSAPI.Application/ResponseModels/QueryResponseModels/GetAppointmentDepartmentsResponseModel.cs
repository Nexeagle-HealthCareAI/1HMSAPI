using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetAppointmentDepartmentsResponseModel
    {
        public List<AppointmentDepartmentInfo> Departments { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class AppointmentDepartmentInfo
    {
        public Guid DepartmentId { get; set; }
        public string DepartmentName { get; set; } = string.Empty;
    }
}
