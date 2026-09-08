using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetPathologyReportKeywordsResponseModel
    {
        public List<PathologyReportKeywordDataModel> Keywords { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class PathologyReportKeywordDataModel
    {
        public Guid KeywordId { get; set; }
        public Guid? TestId { get; set; }
        // Null when TestId is null (a global keyword) -- resolved server-side so the management
        // list doesn't need a second round-trip against the test catalog just to show a name.
        public string? TestName { get; set; }
        public string Keyword { get; set; } = null!;
        public string ContentJson { get; set; } = null!;
        public bool IsActive { get; set; }
    }
}
