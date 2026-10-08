using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.ResponseModels.QueryResponseModels
{
    // An article as the CMS sees it. The text fields are the WORKING COPY: the open edit when a published
    // article has one, otherwise the live text. Status is always the live article's status.
    [ExcludeFromCodeCoverage]
    public class HealthArticleAdminInfo
    {
        public string Slug { get; set; } = null!;
        public string Type { get; set; } = null!;
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public string Content { get; set; } = null!;
        public string? RelatedConditionSlug { get; set; }
        public string? CoverImageUrl { get; set; }
        public string? CoverImageAlt { get; set; }
        public string? Disclosure { get; set; }
        public string? References { get; set; }
        public Guid? AuthorContributorId { get; set; }
        public Guid? ReviewerContributorId { get; set; }
        public string Status { get; set; } = null!;
        // Why the article (or its edit) was sent back.
        public string? ReviewerComment { get; set; }
        public DateTime? PublishedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        // A published article with an edit waiting for approval. The live text is unchanged until it is approved.
        public bool HasPendingRevision { get; set; }
        // DRAFT | IN_REVIEW, null when there is no open edit.
        public string? RevisionStatus { get; set; }
        public long ViewCount { get; set; }
        public long LikeCount { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GetHealthArticlesAdminResponseModel
    {
        public List<HealthArticleAdminInfo> Articles { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class HealthArticleHistoryEntry
    {
        public DateTime At { get; set; }
        public string Actor { get; set; } = null!;
        public string Action { get; set; } = null!;
        public string? Detail { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GetHealthArticleHistoryResponseModel
    {
        public bool Found { get; set; }
        public List<HealthArticleHistoryEntry> Entries { get; set; } = new();
    }

    [ExcludeFromCodeCoverage]
    public class GetHealthWikiConditionsResponseModel
    {
        public List<string> Conditions { get; set; } = new();
    }

    // Numbers on the Health Wiki section tabs in the CMS.
    [ExcludeFromCodeCoverage]
    public class GetHealthWikiSummaryResponseModel
    {
        public int PendingContributors { get; set; }
        public int OpenTopics { get; set; }
    }
}
