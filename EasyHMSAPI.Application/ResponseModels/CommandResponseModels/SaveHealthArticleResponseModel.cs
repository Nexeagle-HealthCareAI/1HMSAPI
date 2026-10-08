using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.CommandResponseModels
{
    [ExcludeFromCodeCoverage]
    public class SaveHealthArticleResponseModel
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public int StatusCode { get; set; } = 200;
        public string? Slug { get; set; }
        public string? Status { get; set; }
        public DateTime? PublishedAt { get; set; }
        // The article after the change, as the CMS shows it.
        public HealthArticleAdminInfo? Article { get; set; }
    }
}
