using System;
using System.Collections.Generic;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    public class GetHrKpiSummaryResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        
        public int TotalStaff { get; set; }
        public int ActiveOnDutyToday { get; set; }
        public int AbsentToday { get; set; }
        public int NursesOnNightShift { get; set; }
        public int OnCallDoctors { get; set; }
        public int PendingLeaveApprovals { get; set; }
        
        public decimal CurrentMonthPayrollTotal { get; set; }
        
        /// <summary>DRAFT | APPROVED | DISBURSED</summary>
        public string PayrollStatus { get; set; } = "DRAFT";
        
        public List<HrLicenseAlertDto> LicenseExpiringSoon { get; set; } = new List<HrLicenseAlertDto>();
    }

    public class HrLicenseAlertDto
    {
        public Guid EmployeeId { get; set; }
        public string EmployeeName { get; set; } = null!;
        public string CredentialType { get; set; } = null!;
        public string ExpiryDate { get; set; } = null!;
        public int DaysUntilExpiry { get; set; }
        public string Severity { get; set; } = null!; // CRITICAL, HIGH, MEDIUM
    }
}
