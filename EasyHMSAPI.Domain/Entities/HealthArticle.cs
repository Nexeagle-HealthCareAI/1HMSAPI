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

        [Key]
        public Guid ArticleId { get; set; }
        public string Slug { get; set; } = null!;
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        // Markdown.
        public string Content { get; set; } = null!;
        public string? RelatedConditionSlug { get; set; }
        // Match the doctorId returned by /public/doctors.
        public Guid? AuthorDoctorId { get; set; }
        public Guid? ReviewerDoctorId { get; set; }
        public string Status { get; set; } = StatusDraft;
        public DateTime? PublishedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
