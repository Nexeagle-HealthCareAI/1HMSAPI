using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    // The "Helpful" button: one like per visitor per article. VisitorKeyHash is a keyed hash, never an IP address or the raw cookie value.
    [ExcludeFromCodeCoverage]
    [Table("HealthArticleLike")]
    public class HealthArticleLike
    {
        public Guid ArticleId { get; set; }
        public string VisitorKeyHash { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
    }
}
