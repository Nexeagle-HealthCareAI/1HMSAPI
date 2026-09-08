using System;
using System.Collections.Generic;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    public class GetLicenseExpiryAlertsResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<string>? Errors { get; set; }
        public List<LicenseAlertDto> Alerts { get; set; } = new List<LicenseAlertDto>();
    }

    public class LicenseAlertDto
    {
        public Guid HrEmployeeId { get; set; }
        public string EmployeeName { get; set; } = null!;
        public string Designation { get; set; } = null!;
        public string DocumentName { get; set; } = null!; // e.g., "Medical Council Registration", "BLS Certification"
        public string ExpiryDate { get; set; } = null!;
        public int DaysLeft { get; set; }
        public string Severity { get; set; } = null!; // CRITICAL, HIGH, MEDIUM
    }
}
