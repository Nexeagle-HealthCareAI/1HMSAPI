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
        public string? SourceType { get; set; }
        public bool IsStat { get; set; }
        // Set when this order was attached to the patient's OPD/IPD billing visit at order time
        // (CreatePathologyOrderHandler / ClinicalOrderCommandHandlers) -- lets the Pathology
        // Workspace show which invoice this order's charges landed on, null when it never had one.
        public Guid? EncounterId { get; set; }
        // Values for the hospital's configured report-level fields -- {key: value}, parsed
        // client-side against LabConfiguration.ReportFieldLayoutJson's "reportFields" list.
        public string? ReportFieldValuesJson { get; set; }

        // Dashboard-list-only fields (populated by GetPathologyOrdersHandler; left at their
        // default 0/null on the single-order GetPathologyOrderByIdHandler response, which exposes
        // the same information via Lines/Report instead) -- lets the Pathology Lab table show test
        // count and report availability/date without a second round-trip per row.
        public int TestCount { get; set; }
        public string? ReportNo { get; set; }
        public DateTime? ReportGeneratedAt { get; set; }
        public string? ReportPdfBlobPath { get; set; }

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
        public string? SampleBarcode { get; set; }
        public DateTime? SampleCollectedAt { get; set; }

        public PathologyResultDto? Result { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class PathologyResultDto
    {
        public Guid ResultId { get; set; }
        public string ResultValuesJson { get; set; } = "{}";
        public string? Interpretation { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class PathologyReportReadyDto
    {
        public string PatientId { get; set; } = null!;
        public Guid ReportId { get; set; }
        public string ReportNo { get; set; } = null!;
        public string OrderNo { get; set; } = null!;
        public DateTime? GeneratedAt { get; set; }
        public string? PdfBlobPath { get; set; }
    }
}
