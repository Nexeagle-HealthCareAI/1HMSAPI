using System;
using System.Collections.Generic;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    public class GetHrAttendanceTodayResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<HrAttendanceLogDto> AttendanceLogs { get; set; } = new List<HrAttendanceLogDto>();
    }

    public class HrAttendanceLogDto
    {
        public Guid HrAttendanceLogId { get; set; }
        public Guid HrEmployeeId { get; set; }
        public string EmployeeName { get; set; } = null!;
        public string EmployeeCode { get; set; } = null!;
        public DateOnly AttendanceDate { get; set; }
        public DateTime? PunchIn { get; set; }
        public DateTime? PunchOut { get; set; }
        public decimal? TotalHoursWorked { get; set; }
        public decimal OvertimeHours { get; set; }
        public string PunchSource { get; set; } = null!;
        public string Status { get; set; } = null!;
        public string? Notes { get; set; }
    }
}
