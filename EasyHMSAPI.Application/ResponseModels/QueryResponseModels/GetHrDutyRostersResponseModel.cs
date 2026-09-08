using System;
using System.Collections.Generic;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    public class GetHrDutyRostersResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<HrDutyRosterDto> Rosters { get; set; } = new List<HrDutyRosterDto>();
    }

    public class HrDutyRosterDto
    {
        public Guid HrDutyRosterId { get; set; }
        public Guid HospitalId { get; set; }
        public Guid EmployeeId { get; set; }
        public string EmployeeName { get; set; } = null!;
        public string EmployeeCode { get; set; } = null!;
        public string DepartmentName { get; set; } = null!;
        public Guid ShiftId { get; set; }
        public string ShiftCode { get; set; } = null!;
        public string ShiftName { get; set; } = null!;
        public DateOnly RosterDate { get; set; }
        public bool IsOnCall { get; set; }
        public Guid? WardId { get; set; }
        public string Status { get; set; } = null!;
        public bool RestPeriodViolation { get; set; }
        public string? ViolationMessage { get; set; }
        public Guid? SwappedWithRosterId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
