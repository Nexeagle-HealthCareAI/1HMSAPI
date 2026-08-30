using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using MediatR;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetPathologyOrdersQuery : IRequest<List<PathologyOrderDto>>
    {
        public Guid HospitalId { get; set; }
        public string? Status { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GetPathologyOrderByIdQuery : IRequest<PathologyOrderDto>
    {
        public Guid HospitalId { get; set; }
        public Guid OrderId { get; set; }
    }

    // Public/anonymous -- deliberately not scoped by HospitalId. A bare QR scan (ReportId only)
    // confirms the report exists and is approved; passing Sha256 too (e.g. typed in from a
    // "Document Hash" line printed on the report, separate from the QR) upgrades the check to a
    // strict byte-for-byte match against the uploaded PDF -- see GetPathologyReportVerificationHandler.
    [ExcludeFromCodeCoverage]
    public class GetPathologyReportVerificationQuery : IRequest<PathologyReportVerificationResponseModel>
    {
        public Guid ReportId { get; set; }
        public string? Sha256 { get; set; }
    }
}
