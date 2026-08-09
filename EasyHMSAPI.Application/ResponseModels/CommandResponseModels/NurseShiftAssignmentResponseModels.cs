using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class AssignNurseShiftResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? NurseShiftAssignmentId { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class ReleaseNurseShiftResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
    }
}
