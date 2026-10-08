using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    // A topic a contributor suggests for the team to commission.
    [ExcludeFromCodeCoverage]
    [Table("HealthWikiTopicRequest")]
    public class HealthWikiTopicRequest
    {
        public const string StatusSubmitted = "SUBMITTED";
        public const string StatusNeedsDetail = "NEEDS_DETAIL";
        public const string StatusAccepted = "ACCEPTED";
        public const string StatusDeclined = "DECLINED";
        public const string StatusArticleStarted = "ARTICLE_STARTED";

        [Key]
        public Guid TopicId { get; set; }
        public Guid ContributorId { get; set; }
        public string Title { get; set; } = null!;
        public string Type { get; set; } = HealthArticle.TypeMedical;
        public string Outline { get; set; } = null!;
        public string? WhyItMatters { get; set; }
        public string? ConditionSlug { get; set; }
        public string? References { get; set; }
        public string Status { get; set; } = StatusSubmitted;
        public string? DecisionNote { get; set; }
        public string? DecidedByName { get; set; }
        public Guid? ArticleId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
