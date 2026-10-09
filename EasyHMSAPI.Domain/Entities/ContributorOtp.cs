using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    // WhatsApp one-time code for a contributor. Only an HMAC of the code is stored.
    [ExcludeFromCodeCoverage]
    [Table("ContributorOtp")]
    public class ContributorOtp
    {
        [Key]
        public Guid OtpId { get; set; }
        public string Mobile { get; set; } = null!;
        public string CodeHash { get; set; } = null!;
        public Guid? LinkId { get; set; }
        public int Attempts { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime? ConsumedAt { get; set; }
        public string? RequestIp { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
