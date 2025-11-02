using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class CreateDepartmentResponseModel
    {
        public Guid DepartmentID { get; set; }
        public Guid HospitalID { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public string? Message { get; set; }
    }
}
