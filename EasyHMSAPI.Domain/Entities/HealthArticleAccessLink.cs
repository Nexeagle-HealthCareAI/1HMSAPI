using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    // Single-use invitation link sent over WhatsApp. Only the SHA-256 of the token is stored.
    // REVIEW / WRITE point at an article; JOIN invites someone to enrol (no article).
    [ExcludeFromCodeCoverage]
    [Table("HealthArticleAccessLink")]
    public class HealthArticleAccessLink
    {
        public const string RoleReview = "REVIEW";
        public const string RoleWrite = "WRITE";
        public const string RoleJoin = "JOIN";

        [Key]
        public Guid LinkId { get; set; }
        public string TokenHash { get; set; } = null!;
        public string Role { get; set; } = null!;
        public Guid? ArticleId { get; set; }
        public Guid? ContributorId { get; set; }
        public string? InviteeName { get; set; }
        // The 10-digit number the link was sent to. The OTP for this link goes only to this number.
        public string Mobile { get; set; } = null!;
        public string? CreatedByName { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime? UsedAt { get; set; }
        public DateTime? RevokedAt { get; set; }
        public int SendCount { get; set; }
        public DateTime? LastSentAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
