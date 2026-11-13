using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    /// <summary>
    /// A single-use, expiring sign-in link issued to one staff member for one hospital (see
    /// MagicLinkService). Only the SHA-256 hash of the token is stored — the raw token exists only
    /// inside the link that was sent.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class MagicLoginToken
    {
        [Key]
        public Guid TokenId { get; set; }
        public string TokenHash { get; set; } = null!;
        public Guid UserId { get; set; }
        public Guid HospitalId { get; set; }
        public string TargetPath { get; set; } = null!;
        public string Purpose { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime? ConsumedAt { get; set; }
        public string? ConsumedIp { get; set; }
        public byte[]? RowVersion { get; set; }
    }
}
