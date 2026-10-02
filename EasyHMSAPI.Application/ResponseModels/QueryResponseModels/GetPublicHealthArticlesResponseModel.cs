using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetPublicHealthArticlesResponseModel
    {
        public List<PublicHealthArticleInfo> Articles { get; set; } = new();
    }

    // Public contract shared with Doctor Dekho. Id is the article slug (used in /health/conditions/{id}).
    [ExcludeFromCodeCoverage]
    public class PublicHealthArticleInfo
    {
        public string Id { get; set; } = null!;
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        // Markdown.
        public string Content { get; set; } = null!;
        public string? RelatedConditionSlug { get; set; }
        public Guid? AuthorDoctorId { get; set; }
        public Guid? ReviewerDoctorId { get; set; }
        public DateTime? PublishedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
