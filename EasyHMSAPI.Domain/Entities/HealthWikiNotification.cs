using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    // A WhatsApp message the Health Wiki sent (or tried to). Code is the product's message id (N1 to N15). Only a masked number is kept.
    [ExcludeFromCodeCoverage]
    [Table("HealthWikiNotification")]
    public class HealthWikiNotification
    {
        public const string StatusSent = "SENT";
        public const string StatusFailed = "FAILED";
        public const string StatusSkipped = "SKIPPED";

        [Key]
        public Guid NotificationId { get; set; }
        public string Code { get; set; } = null!;
        public string TemplateName { get; set; } = null!;
        public Guid? RecipientContributorId { get; set; }
        public string? RecipientMobileMasked { get; set; }
        // ARTICLE | CONTRIBUTOR | TOPIC
        public string EntityType { get; set; } = null!;
        public Guid EntityId { get; set; }
        public string Status { get; set; } = null!;
        public string? Detail { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
