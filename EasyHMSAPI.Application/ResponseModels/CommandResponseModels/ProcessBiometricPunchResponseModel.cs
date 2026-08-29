using System;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    public class ProcessBiometricPunchResponseModel
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public Guid? AttendanceLogId { get; set; }
    }
}
