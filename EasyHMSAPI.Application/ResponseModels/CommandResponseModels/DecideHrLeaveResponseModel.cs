using System;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    public class DecideHrLeaveResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid LeaveId { get; set; }
        public string Status { get; set; } = null!;
        /// <summary>Days marked ON_LEAVE on approval (skips any day that already had an attendance row).</summary>
        public int AttendanceDaysUpdated { get; set; }
    }
}
