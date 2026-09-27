using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class IngestBiometricPunchesResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        /// <summary>True when the device credentials were not accepted; the controller answers 401.</summary>
        public bool Unauthorized { get; set; }
        public int Received { get; set; }
        public int Accepted { get; set; }
        public int Duplicates { get; set; }
        public int Invalid { get; set; }
        public int Unmapped { get; set; }
        public int AttendanceDaysUpdated { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class RegisterHrBiometricDeviceResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public HrBiometricDeviceDto? Device { get; set; }
        /// <summary>The device's access token. Returned ONCE, here; only its hash is stored.</summary>
        public string? Token { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class RotateHrBiometricDeviceTokenResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        /// <summary>The new token, shown once. The previous token stops working immediately.</summary>
        public string? Token { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class HrBiometricActionResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class MapEmployeeDeviceUserResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        /// <summary>Attendance days built from scans that were waiting for this PIN to be mapped.</summary>
        public int AttendanceDaysUpdated { get; set; }
    }
}
