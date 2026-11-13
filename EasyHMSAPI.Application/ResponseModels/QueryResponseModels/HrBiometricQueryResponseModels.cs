using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class HrBiometricDeviceDto
    {
        public Guid HrBiometricDeviceId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string Vendor { get; set; } = string.Empty;
        public string? Model { get; set; }
        public string? Location { get; set; }
        public bool IsActive { get; set; }
        public DateTime? LastSeenAt { get; set; }
        public string? LastSeenIp { get; set; }
        public DateTime? LastPunchTime { get; set; }
        /// <summary>Heard from within the last 10 minutes.</summary>
        public bool IsOnline { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GetHrBiometricDevicesResponseModel
    {
        public bool Success { get; set; }
        public List<HrBiometricDeviceDto> Devices { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class UnmappedDeviceUserDto
    {
        public string DeviceUserId { get; set; } = string.Empty;
        public int ScanCount { get; set; }
        public DateTime FirstSeen { get; set; }
        public DateTime LastSeen { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GetUnmappedDeviceUsersResponseModel
    {
        public bool Success { get; set; }
        public List<UnmappedDeviceUserDto> Users { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class EmployeeDeviceUserDto
    {
        public Guid HrEmployeeId { get; set; }
        public string EmployeeCode { get; set; } = string.Empty;
        public string EmployeeName { get; set; } = string.Empty;
        public string DeviceUserId { get; set; } = string.Empty;
    }

    [ExcludeFromCodeCoverage]
    public class GetEmployeeDeviceUsersResponseModel
    {
        public bool Success { get; set; }
        public List<EmployeeDeviceUserDto> Links { get; set; } = new();
    }
}
