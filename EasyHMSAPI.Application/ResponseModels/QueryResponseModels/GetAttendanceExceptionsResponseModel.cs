using System;
using System.Collections.Generic;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    public class GetAttendanceExceptionsResponseModel
    {
        public List<AttendanceExceptionDto> Exceptions { get; set; } = new List<AttendanceExceptionDto>();
    }

    public class AttendanceExceptionDto
    {
        public Guid AttendanceLogId { get; set; }
        public Guid EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string EmployeeCode { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public DateTime AttendanceDate { get; set; }
        public DateTime? PunchIn { get; set; }
        public DateTime? PunchOut { get; set; }
        public string ExceptionType { get; set; } = string.Empty; // "LATE", "MISSING_OUT_PUNCH", "UNSCHEDULED"
        public string Description { get; set; } = string.Empty;
    }
}
