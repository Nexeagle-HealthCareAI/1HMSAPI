using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    [ExcludeFromCodeCoverage]
    public class GetPublicHealthArticlesResponseModel
    {
        public List<PublicHealthArticleInfo> Articles { get; set; } = new();
    }

    // Public contract shared with Doctor Dekho (docs/health-wiki-api-contract.md there). Id is the article slug
    // (used in /health/conditions/{id}). Contributor mobile and email are never part of it.
    [ExcludeFromCodeCoverage]
    public class PublicHealthArticleInfo
    {
        public string Id { get; set; } = null!;
        // MEDICAL | SECTOR_UPDATE
        public string Type { get; set; } = null!;
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        // Markdown.
        public string Content { get; set; } = null!;
        public string? CoverImageUrl { get; set; }
        public string? CoverImageAlt { get; set; }
        public string? RelatedConditionSlug { get; set; }
        public string? Disclosure { get; set; }
        // One source per line.
        public string? References { get; set; }
        public PublicArticlePerson? Author { get; set; }
        // Only on a MEDICAL article. The page shows the badge only when IsRegistrationVerified is true.
        public PublicArticlePerson? Reviewer { get; set; }
        public DateTime? PublishedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class PublicArticlePerson
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = null!;
        public string Type { get; set; } = null!;
        public string? Speciality { get; set; }
        public string? Qualification { get; set; }
        public string? RoleTitle { get; set; }
        public string? Organisation { get; set; }
        public string? PhotoUrl { get; set; }
        // Reviewer only.
        public string? RegistrationNumber { get; set; }
        public string? RegistrationCouncil { get; set; }
        public bool? IsRegistrationVerified { get; set; }
    }
}
