using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    // One article in a hospital doctor's Health Wiki inbox (EasyHMS).
    [ExcludeFromCodeCoverage]
    public class HealthWikiInboxItem
    {
        public string Slug { get; set; } = null!;
        public string Type { get; set; } = null!;
        public string Title { get; set; } = null!;
        public string Description { get; set; } = string.Empty;
        public string? CoverImageUrl { get; set; }
        public string AuthorName { get; set; } = string.Empty;
        // Whole days an article in review has been waiting. Null once it is published.
        public int? WaitingDays { get; set; }
        public long? Views { get; set; }
        public long? Likes { get; set; }
        public DateTime? PublishedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GetHealthWikiInboxResponseModel
    {
        public bool Success { get; set; }
        public int StatusCode { get; set; } = 200;
        public string? Message { get; set; }
        public List<HealthWikiInboxItem> Pending { get; set; } = new();
        public List<HealthWikiInboxItem> Published { get; set; } = new();

        public static GetHealthWikiInboxResponseModel Fail(int status, string message) => new() { Success = false, StatusCode = status, Message = message };
    }

    // The signed-in doctor as the "Medically reviewed by" badge will show them.
    [ExcludeFromCodeCoverage]
    public class HealthWikiReviewerBadge
    {
        public string Name { get; set; } = null!;
        public string? Qualification { get; set; }
        public string? Speciality { get; set; }
        public string? RegistrationNumber { get; set; }
        public string? RegistrationCouncil { get; set; }
    }

    // An article for review or for its numbers, as the assigned doctor sees it. Flat on the wire: the inbox fields plus these.
    [ExcludeFromCodeCoverage]
    public class HealthWikiStaffArticleInfo : HealthWikiInboxItem
    {
        // Markdown.
        public string Content { get; set; } = null!;
        public string? CoverImageAlt { get; set; }
        public string? RelatedConditionSlug { get; set; }
        public string? Disclosure { get; set; }
        // One source per line.
        public string? References { get; set; }
        public HealthWikiReviewerBadge Reviewer { get; set; } = null!;
        // IN_REVIEW (including an edit to a live article) | PUBLISHED
        public string Status { get; set; } = null!;
        // The signed-in doctor is the assigned reviewer and it is waiting for them.
        public bool CanDecide { get; set; }
        // Views for each of the last 14 days, oldest first. Empty while the article is in review.
        public List<int> DailyViews { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class GetHealthWikiStaffArticleResponseModel
    {
        public bool Success { get; set; }
        public int StatusCode { get; set; } = 200;
        public string? Message { get; set; }
        public HealthWikiStaffArticleInfo? Article { get; set; }

        public static GetHealthWikiStaffArticleResponseModel Fail(int status, string message) => new() { Success = false, StatusCode = status, Message = message };
    }
}
