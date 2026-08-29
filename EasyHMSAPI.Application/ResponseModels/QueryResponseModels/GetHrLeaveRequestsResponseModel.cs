using System;
using System.Collections.Generic;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    public class GetHrLeaveRequestsResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<HrLeaveRequestDto> LeaveRequests { get; set; } = new List<HrLeaveRequestDto>();
    }

    public class HrLeaveRequestDto
    {
        public Guid HrLeaveRequestId { get; set; }
        public Guid HrEmployeeId { get; set; }
        public string EmployeeName { get; set; } = null!;
        public string EmployeeCode { get; set; } = null!;
        public string LeaveType { get; set; } = null!;
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public decimal TotalDays { get; set; }
        public string Reason { get; set; } = null!;
        public string Status { get; set; } = null!;
        public Guid? ApprovedByUserId { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string? MedicalCertificateUrl { get; set; }
        public string? RejectionReason { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
