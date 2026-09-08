using System;
using System.Collections.Generic;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    public class GetHrHospitalShiftsResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<HrHospitalShiftDto> Shifts { get; set; } = new List<HrHospitalShiftDto>();
    }

    public class HrHospitalShiftDto
    {
        public Guid HrHospitalShiftId { get; set; }
        public Guid HospitalId { get; set; }
        public string ShiftCode { get; set; } = null!;
        public string ShiftName { get; set; } = null!;
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public int GracePeriodMinutes { get; set; }
        public int HandoverBufferMinutes { get; set; }
        public decimal NightAllowanceAmount { get; set; }
        public decimal CalloutFeeAmount { get; set; }
        public bool IsActive { get; set; }
        public string? ApplicableRolesJson { get; set; }
    }
}
