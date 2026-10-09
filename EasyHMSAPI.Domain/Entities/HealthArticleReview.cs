using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    // One decision by an article's doctor reviewer, kept with the name and registration as they were that day.
    [ExcludeFromCodeCoverage]
    [Table("HealthArticleReview")]
    public class HealthArticleReview
    {
        [Key]
        public Guid ReviewId { get; set; }
        public Guid ArticleId { get; set; }
        public Guid ReviewerContributorId { get; set; }
        // APPROVE | REQUEST_CHANGES
        public string Decision { get; set; } = null!;
        public string? Comment { get; set; }
        public bool AccuracyConfirmed { get; set; }
        public string ReviewerNameSnapshot { get; set; } = null!;
        public string? RegistrationSnapshot { get; set; }
        public DateTime DecidedAt { get; set; }
    }
}
