using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    // Health Wiki article served read-only to Doctor Dekho (GET /public/health-articles).
    // Authored in the CMS, doctor-reviewed in EasyHMS, written via /internal/health-articles.
    // Only Status == PUBLISHED rows are ever exposed publicly. Slug is the public id.
    [ExcludeFromCodeCoverage]
    [Table("HealthArticle")]
    public class HealthArticle
    {
        public const string StatusDraft = "DRAFT";
        public const string StatusInReview = "IN_REVIEW";
        public const string StatusPublished = "PUBLISHED";

        public const string TypeMedical = "MEDICAL";
        public const string TypeSectorUpdate = "SECTOR_UPDATE";

        [Key]
        public Guid ArticleId { get; set; }
        public string Slug { get; set; } = null!;
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        // Markdown.
        public string Content { get; set; } = null!;
        public string? RelatedConditionSlug { get; set; }
        // MEDICAL (needs a verified doctor reviewer, shows the badge) or SECTOR_UPDATE (CMS editor approves, never a reviewer).
        public string Type { get; set; } = TypeMedical;
        public string? CoverImageUrl { get; set; }
        public string? CoverImageAlt { get; set; }
        public string? Disclosure { get; set; }
        // One source per line.
        public string? References { get; set; }
        public Guid? AuthorContributorId { get; set; }
        public Guid? ReviewerContributorId { get; set; }
        // Why the article was sent back for changes.
        public string? ReviewerComment { get; set; }
        public DateTime? SubmittedAt { get; set; }
        // The reviewer approved (a MEDICAL article is published only after this and a VERIFIED reviewer).
        public DateTime? ApprovedAt { get; set; }
        public string? ApprovedByName { get; set; }
        public long ViewCount { get; set; }
        public long LikeCount { get; set; }
        // DEPRECATED: replaced by the contributor ids above. Kept only so the old column mapping stays valid until a later migration drops them.
        public Guid? AuthorDoctorId { get; set; }
        public Guid? ReviewerDoctorId { get; set; }
        public string Status { get; set; } = StatusDraft;
        public DateTime? PublishedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
