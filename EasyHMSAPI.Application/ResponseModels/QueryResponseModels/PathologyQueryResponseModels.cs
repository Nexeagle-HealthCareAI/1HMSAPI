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
        public string? PatientMobile { get; set; }
        public int? PatientAgeYears { get; set; }
        public string? PatientGender { get; set; }
        public string? HospitalName { get; set; }
        public string? SourceType { get; set; }
        public bool IsStat { get; set; }
        // Daily, per-hospital token (resets every day) for the thermal-printed receipt -- separate
        // from OrderNo. Null for orders created before this feature shipped.
        public int? TokenNumber { get; set; }
        public string? Notes { get; set; }
        // Set when this order was attached to the patient's OPD/IPD billing visit at order time
        // (CreatePathologyOrderHandler / ClinicalOrderCommandHandlers) -- lets the Pathology
        // Workspace show which invoice this order's charges landed on, null when it never had one.
        public Guid? EncounterId { get; set; }
        // Set for an IPD order instead of EncounterId -- lets the order-edit flow re-select the same
        // admission by default rather than losing track of which one an IPD order was placed under.
        public Guid? AdmissionId { get; set; }
        // Values for the hospital's configured report-level fields -- {key: value}, parsed
        // client-side against LabConfiguration.ReportFieldLayoutJson's "reportFields" list.
        public string? ReportFieldValuesJson { get; set; }

        // Dashboard-list-only fields (populated by GetPathologyOrdersHandler; left at their
        // default 0 on the single-order GetPathologyOrderByIdHandler response, which exposes the
        // same information per-line via Lines[].Report instead) -- lets the Pathology Lab table
        // show test count and how many of this order's tests have a report ready, without a second
        // round-trip per row.
        public int TestCount { get; set; }
        public int ReportsReadyCount { get; set; }
        // Test names on this order (e.g. "CBC", "LFT") -- dashboard-list-only, resolved in a
        // second batched query rather than a nested EF projection.
        public List<string> TestNames { get; set; } = new();

        public List<PathologyOrderLineDto> Lines { get; set; } = new();
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
        // This line's own report -- each PathologyOrderLine (test) now gets its own independent
        // report rather than sharing one report for the whole order (see
        // GeneratePathologyReportHandler). Null until a report has been generated for this test.
        public PathologyReportDto? Report { get; set; }
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
        // Which test this report covers -- a patient can now have more than one ready report per
        // order (one per test line), so the badge/dialog showing these needs a way to tell them apart.
        public string? TestName { get; set; }
    }
}
