using System;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class UpsertPathologyReportKeywordResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public Guid KeywordId { get; set; }
    }
}
