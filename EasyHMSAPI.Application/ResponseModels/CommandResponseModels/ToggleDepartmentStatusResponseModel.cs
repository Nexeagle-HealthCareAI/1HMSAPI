using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class ToggleDepartmentStatusResponseModel
    {
        public Guid DepartmentID { get; set; }
        public bool IsActive { get; set; }
        public string? Message { get; set; }
    }
}
