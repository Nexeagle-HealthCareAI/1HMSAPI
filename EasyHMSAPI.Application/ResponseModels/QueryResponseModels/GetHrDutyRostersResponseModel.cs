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
        public Guid HrEmployeeId { get; set; }
        public Guid HrHospitalShiftId { get; set; }
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
