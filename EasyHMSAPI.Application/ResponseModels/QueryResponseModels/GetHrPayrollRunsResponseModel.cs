using System;
using System.Collections.Generic;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    public class GetHrPayrollRunsResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<string>? Errors { get; set; }
        public int TotalCount { get; set; }
        public List<HrPayrollRunDto> PayrollRuns { get; set; } = new List<HrPayrollRunDto>();
    }

    public class HrPayrollRunDto
    {
        public Guid HrPayrollRunId { get; set; }
        public Guid HospitalId { get; set; }
        public int Month { get; set; }
        public int Year { get; set; }
        public string Status { get; set; } = null!;
        public decimal TotalGrossDisbursement { get; set; }
        public decimal TotalNetDisbursement { get; set; }
        public decimal TotalPfDeducted { get; set; }
        public decimal TotalEsiDeducted { get; set; }
        public decimal TotalTdsDeducted { get; set; }
        public Guid? ProcessedByUserId { get; set; }
        public DateTime ProcessedAt { get; set; }
    }
}
