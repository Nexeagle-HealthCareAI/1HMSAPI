using System;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GeneratePathologyReportResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid? ReportId { get; set; }
        public string? ReportNo { get; set; }
    }
}
