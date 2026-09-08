using System;
using System.Collections.Generic;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    public class GetPayslipsByRunResponseModel
    {
        public List<HrPayslipDto> Payslips { get; set; } = new List<HrPayslipDto>();
    }

    public class HrPayslipDto
    {
        public Guid HrPayslipId { get; set; }
        public string PayslipNumber { get; set; } = string.Empty;
        public string EmployeeName { get; set; } = string.Empty;
        public string EmployeeCode { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string Designation { get; set; } = string.Empty;
        public string PayrollTrack { get; set; } = string.Empty;
        public string PanNumber { get; set; } = string.Empty;
        public string? UanNumber { get; set; }
        public string? BankName { get; set; }
        public string? BankAccountNumber { get; set; }
        
        // Stats
        public int TotalDaysInMonth { get; set; }
        public decimal PayableDays { get; set; }
        public decimal OvertimeDays { get; set; }
        public int NightShiftCount { get; set; }
        
        // Earnings
        public decimal BasicEarned { get; set; }
        public decimal HraEarned { get; set; }
        public decimal AllowancesEarned { get; set; }
        public decimal OvertimeAmount { get; set; }
        public decimal NightAllowanceAmount { get; set; }
        public decimal IncentivesAmount { get; set; }
        public decimal RetainerAmount { get; set; }
        public decimal OpdShareAmount { get; set; }
        public decimal IpdVisitAmount { get; set; }
        public decimal SurgeryShareAmount { get; set; }
        public decimal GrossEarnings { get; set; }
        
        // Deductions
        public decimal PfEmployee { get; set; }
        public decimal EsiEmployee { get; set; }
        public decimal ProfTax { get; set; }
        public decimal TdsDeducted { get; set; }
        public decimal LoanInstallment { get; set; }
        public decimal TotalDeductions { get; set; }
        
        // Net
        public decimal NetSalary { get; set; }
    }
}
