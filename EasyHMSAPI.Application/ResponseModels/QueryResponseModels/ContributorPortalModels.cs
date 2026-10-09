using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    // The signed-in contributor. The mobile number is masked.
    [ExcludeFromCodeCoverage]
    public class ContributorMeInfo
    {
        public Guid Id { get; set; }
        // INDEPENDENT_DOCTOR | HEALTH_WORKER | WRITER
        public string Role { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string MobileMasked { get; set; } = null!;
        public string Status { get; set; } = null!;
        public string? Speciality { get; set; }
        public string? Qualification { get; set; }
        public string? RoleTitle { get; set; }
        public string? Organisation { get; set; }
        public string? RegistrationNumber { get; set; }
        public string? RegistrationCouncil { get; set; }
        public string? RejectReason { get; set; }
    }

    // An article as the contributor sees it: one they wrote, or one they must review.
    [ExcludeFromCodeCoverage]
    public class MyArticleInfo
    {
        public string Slug { get; set; } = null!;
        public string Type { get; set; } = null!;
        public string Title { get; set; } = null!;
        public string Description { get; set; } = string.Empty;
        public string Content { get; set; } = null!;
        public string? CoverImageUrl { get; set; }
        public string? CoverImageAlt { get; set; }
        public string? RelatedConditionSlug { get; set; }
        public string? Disclosure { get; set; }
        public string? References { get; set; }
        public string Status { get; set; } = null!;
        // AUTHOR | REVIEWER
        public string MyRole { get; set; } = null!;
        public string AuthorName { get; set; } = string.Empty;
        public string? ReviewerName { get; set; }
        public string? ReturnedComment { get; set; }
        public int? WaitingDays { get; set; }
        // The reviewer approved, but their registration is not verified yet, so the article is not live.
        public bool AwaitingVerification { get; set; }
        public long? Views { get; set; }
        public long? Likes { get; set; }
        public DateTime? PublishedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        // A live article with an edit waiting. Readers still see the live text.
        public bool HasPendingRevision { get; set; }
        public string? RevisionStatus { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class MyArticleListInfo
    {
        public List<MyArticleInfo> Pending { get; set; } = new();
        public List<MyArticleInfo> Published { get; set; } = new();
        public List<MyArticleInfo> Drafts { get; set; } = new();
    }

    // A contributor's own topic request.
    [ExcludeFromCodeCoverage]
    public class MyTopicInfo
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = null!;
        public string Type { get; set; } = null!;
        public string Outline { get; set; } = null!;
        public string WhyItMatters { get; set; } = string.Empty;
        public string? ConditionSlug { get; set; }
        public string? References { get; set; }
        public string Status { get; set; } = null!;
        public string? DecisionNote { get; set; }
        public string? ArticleSlug { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    // A topic request as the CMS sees it.
    [ExcludeFromCodeCoverage]
    public class TopicRequestAdminInfo
    {
        public Guid TopicId { get; set; }
        public Guid ContributorId { get; set; }
        public string Title { get; set; } = null!;
        public string Type { get; set; } = null!;
        public string Outline { get; set; } = null!;
        public string WhyItMatters { get; set; } = string.Empty;
        public string? ConditionSlug { get; set; }
        public string? References { get; set; }
        public string Status { get; set; } = null!;
        // Why it was declined, or what detail the team asked for.
        public string? DecisionReason { get; set; }
        public string? ArticleSlug { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
