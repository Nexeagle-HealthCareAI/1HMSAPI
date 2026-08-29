using System;
using System.Collections.Generic;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    public class RunMonthlyPayrollResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<string>? Errors { get; set; }
        public Guid HrPayrollRunId { get; set; }
        public int PayslipsGenerated { get; set; }
        public decimal TotalNetDisbursement { get; set; }
    }
}
