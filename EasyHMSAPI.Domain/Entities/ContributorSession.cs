using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    // A signed-in contributor. The browser holds an HttpOnly cookie on the Doctor Dekho site; only its SHA-256 is stored here.
    [ExcludeFromCodeCoverage]
    [Table("ContributorSession")]
    public class ContributorSession
    {
        [Key]
        public Guid SessionId { get; set; }
        public Guid ContributorId { get; set; }
        public string TokenHash { get; set; } = null!;
        public DateTime ExpiresAt { get; set; }
        public DateTime? LastSeenAt { get; set; }
        public DateTime? RevokedAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
