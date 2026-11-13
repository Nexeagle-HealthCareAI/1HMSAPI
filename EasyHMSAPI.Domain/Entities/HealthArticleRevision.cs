using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    // Working copy of an edit to a PUBLISHED article. The live article is untouched until the revision is approved and applied.
    // At most one open (DRAFT / IN_REVIEW) revision per article.
    [ExcludeFromCodeCoverage]
    [Table("HealthArticleRevision")]
    public class HealthArticleRevision
    {
        public const string StatusDraft = "DRAFT";
        public const string StatusInReview = "IN_REVIEW";
        public const string StatusApplied = "APPLIED";
        public const string StatusDiscarded = "DISCARDED";

        [Key]
        public Guid RevisionId { get; set; }
        public Guid ArticleId { get; set; }
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public string Content { get; set; } = null!;
        public string? RelatedConditionSlug { get; set; }
        public string? CoverImageUrl { get; set; }
        public string? CoverImageAlt { get; set; }
        public string? Disclosure { get; set; }
        public string? References { get; set; }
        public Guid? EditedByContributorId { get; set; }
        public string? EditedByName { get; set; }
        public string Status { get; set; } = StatusDraft;
        public string? ReviewerComment { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }

        public bool IsOpen => Status == StatusDraft || Status == StatusInReview;
    }
}
