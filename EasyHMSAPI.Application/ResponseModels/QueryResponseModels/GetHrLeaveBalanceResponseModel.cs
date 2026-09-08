using System;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    public class GetHrLeaveBalanceResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public HrLeaveBalanceDto? LeaveBalance { get; set; }
    }

    public class HrLeaveBalanceDto
    {
        public Guid HrLeaveBalanceId { get; set; }
        public Guid HrEmployeeId { get; set; }
        public int Year { get; set; }
        public decimal CasualLeaveBalance { get; set; }
        public decimal SickLeaveBalance { get; set; }
        public decimal EarnedLeaveBalance { get; set; }
        public decimal CompOffBalance { get; set; }
        public decimal MaternityLeaveBalance { get; set; }
        public decimal CmeLeaveBalance { get; set; }
        public decimal CasualLeaveUsed { get; set; }
        public decimal SickLeaveUsed { get; set; }
        public decimal EarnedLeaveUsed { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
