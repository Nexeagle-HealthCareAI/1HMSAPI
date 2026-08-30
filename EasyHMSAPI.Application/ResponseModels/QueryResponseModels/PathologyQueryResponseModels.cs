using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using EasyHMSAPI.Domain.Entities;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class PathologyOrderDto
    {
        public Guid OrderId { get; set; }
        public string OrderNo { get; set; } = null!;
        public DateTime OrderDate { get; set; }
        public string Status { get; set; } = null!;
        public string PatientId { get; set; } = null!;
        public string PatientName { get; set; } = null!;
        public int? PatientAgeYears { get; set; }
        public string? PatientGender { get; set; }
        public string? HospitalName { get; set; }

        public List<PathologyOrderLineDto> Lines { get; set; } = new();
        // Present once GeneratePathologyReportHandler has created a report for this order --
        // single source of truth for the dual-signature UI so it survives a page reload instead of
        // relying on local component state left over from whichever action the browser just ran.
        public PathologyReportDto? Report { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class PathologyReportDto
    {
        public Guid ReportId { get; set; }
        public string ReportNo { get; set; } = null!;
        public string Status { get; set; } = null!;
        public DateTime? GeneratedAt { get; set; }
        public string? TechnicianName { get; set; }
        public string? TechnicianRegNo { get; set; }
        public DateTime? TechnicianSignedAt { get; set; }
        public string? PathologistName { get; set; }
        public string? PathologistRegNo { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string? PdfBlobPath { get; set; }
        public string? PdfSha256 { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class PathologyOrderLineDto
    {
        public Guid OrderLineId { get; set; }
        public Guid TestId { get; set; }
        public string TestName { get; set; } = null!;
        public string TestCode { get; set; } = null!;
        public string Status { get; set; } = null!;
        public string? ParameterSchemaJson { get; set; }
        
        public PathologyResultDto? Result { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class PathologyResultDto
    {
        public Guid ResultId { get; set; }
        public string ResultValuesJson { get; set; } = "{}";
        public string? Interpretation { get; set; }
    }

    // Deliberately excludes every field that identifies the patient -- this is served to whoever
    // scans the QR code on a printed/shared report, not just the patient themselves.
    [ExcludeFromCodeCoverage]
    public class PathologyReportVerificationResponseModel
    {
        public bool IsAuthentic { get; set; }
        public string Message { get; set; } = null!;
        public string? ReportNo { get; set; }
        public string? HospitalName { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string? TechnicianName { get; set; }
        public string? PathologistName { get; set; }
    }
}
