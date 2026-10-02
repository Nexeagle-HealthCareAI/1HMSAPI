using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class UpdateDepartmentResponseModel
    {
        /// <summary>True when the caller is not authorised for this action (controller maps to 403).</summary>
        public bool Forbidden { get; set; }
        public Guid DepartmentID { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public bool Success { get; set; }
        public string? Message { get; set; }
    }
}
