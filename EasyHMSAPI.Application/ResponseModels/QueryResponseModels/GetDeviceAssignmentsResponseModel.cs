using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetDeviceAssignmentsResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<DeviceAssignmentItem> Devices { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class DeviceAssignmentItem
    {
        public Guid DeviceAssignmentId { get; set; }
        public string DeviceType { get; set; } = null!;
        public string? InsertionSite { get; set; }
        public string? Indication { get; set; }
        public string InsertedByDoctorName { get; set; } = null!;
        public DateTime InsertedAt { get; set; }
        public DateTime? RemovedAt { get; set; }
        public string? RemovedBy { get; set; }
        public string? RemovalReason { get; set; }
        public string StatusCode { get; set; } = null!;
        public string? Notes { get; set; }
        public int DaysInSitu { get; set; }
    }
}
