using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    // A hospital doctor passed from EasyHMS to Doctor Dekho already signed in. Single use and short lived; only the SHA-256 of the code is stored.
    [ExcludeFromCodeCoverage]
    [Table("ContributorHandoff")]
    public class ContributorHandoff
    {
        [Key]
        public Guid HandoffId { get; set; }
        public Guid ContributorId { get; set; }
        public string TokenHash { get; set; } = null!;
        public DateTime ExpiresAt { get; set; }
        public DateTime? UsedAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
