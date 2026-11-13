using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    public class SetAttendanceOverrideRequestModel : IRequest<SetAttendanceOverrideResponseModel>
    {
        public Guid HrEmployeeId { get; set; }
        public DateOnly AttendanceDate { get; set; }
        /// <summary>PRESENT | LATE | HALF_DAY | ABSENT | ON_LEAVE</summary>
        public string Status { get; set; } = string.Empty;
        /// <summary>Left null to leave an existing punch time untouched.</summary>
        public DateTime? PunchIn { get; set; }
        public DateTime? PunchOut { get; set; }
        public string? Notes { get; set; }
        /// <summary>Required — why this correction was made, kept for audit.</summary>
        public string Reason { get; set; } = string.Empty;
        /// <summary>Caller identity: set by the controller, never client-supplied.</summary>
        public Guid LoggedInUserId { get; set; }
    }
}
