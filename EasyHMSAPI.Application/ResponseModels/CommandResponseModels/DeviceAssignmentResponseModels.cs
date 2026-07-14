using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class InsertDeviceResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? DeviceAssignmentId { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class RemoveDeviceResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? DeviceAssignmentId { get; set; }
    }
}
