using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    /// <summary>One scan as sent by a device or an on-site bridge.</summary>
    [ExcludeFromCodeCoverage]
    public class BiometricPunchDto
    {
        /// <summary>The user's PIN on the device.</summary>
        public string? UserId { get; set; }
        /// <summary>The device's wall-clock time for the scan (no timezone).</summary>
        public DateTime Time { get; set; }
        /// <summary>Optional: the device's check-in/out key. Informational only.</summary>
        public int? State { get; set; }
        /// <summary>Optional: verify method (fingerprint / card / face / PIN). Informational only.</summary>
        public int? Verify { get; set; }
    }

    /// <summary>A device (or bridge) delivering scans. Authenticated by registered serial + token.</summary>
    [ExcludeFromCodeCoverage]
    public class IngestBiometricPunchesRequestModel : IRequest<IngestBiometricPunchesResponseModel>
    {
        public string? DeviceSerial { get; set; }
        public string? DeviceToken { get; set; }
        public string? RemoteIp { get; set; }
        public List<BiometricPunchDto> Punches { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class RegisterHrBiometricDeviceRequestModel : IRequest<RegisterHrBiometricDeviceResponseModel>
    {
        public Guid HospitalId { get; set; }
        public string? Name { get; set; }
        public string? SerialNumber { get; set; }
        public string? Model { get; set; }
        public string? Location { get; set; }
        /// <summary>Caller identity: set by the controller, never client-supplied.</summary>
        public Guid LoggedInUserId { get; set; }
        public string? LoggedInUserName { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class SetHrBiometricDeviceActiveRequestModel : IRequest<HrBiometricActionResponseModel>
    {
        public Guid HrBiometricDeviceId { get; set; }
        public bool IsActive { get; set; }
        public Guid LoggedInUserId { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class RotateHrBiometricDeviceTokenRequestModel : IRequest<RotateHrBiometricDeviceTokenResponseModel>
    {
        public Guid HrBiometricDeviceId { get; set; }
        public Guid LoggedInUserId { get; set; }
    }

    /// <summary>Links an employee to the PIN they were enrolled under on the device(s).</summary>
    [ExcludeFromCodeCoverage]
    public class MapEmployeeDeviceUserRequestModel : IRequest<MapEmployeeDeviceUserResponseModel>
    {
        public Guid HrEmployeeId { get; set; }
        public string? DeviceUserId { get; set; }
        public Guid LoggedInUserId { get; set; }
        public string? LoggedInUserName { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class UnmapEmployeeDeviceUserRequestModel : IRequest<HrBiometricActionResponseModel>
    {
        public Guid HrEmployeeId { get; set; }
        public Guid LoggedInUserId { get; set; }
    }
}
