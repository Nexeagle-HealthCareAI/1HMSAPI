using System;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    public class SetAttendanceOverrideResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? AttendanceLogId { get; set; }
    }
}
