using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EasyHMSAPI.Domain.Entities
{
    public class UserInvitation
    {
        [Key]
        public Guid InvitationID { get; set; }

        public Guid HospitalID { get; set; }
        public Guid RoleID { get; set; }
        public Guid InvitedByUserID { get; set; }

        [MaxLength(150)]
        public string? RecipientName { get; set; }

        [MaxLength(20)]
        public string RecipientMobile { get; set; } = null!;

        [MaxLength(150)]
        public string? RecipientEmail { get; set; }

        [MaxLength(64)]
        public byte[] TokenHash { get; set; } = null!;

        public DateTime ExpiresAt { get; set; }
        public DateTime? AcceptedAt { get; set; }
        public DateTime? RevokedAt { get; set; }

        [MaxLength(20)]
        public string Status { get; set; } = null!;

        public DateTime? CreatedAt { get; set; }

        public Hospital Hospital { get; set; } = null!;
        public Role Role { get; set; } = null!;
        public User InvitedByUser { get; set; } = null!;
    }
}
