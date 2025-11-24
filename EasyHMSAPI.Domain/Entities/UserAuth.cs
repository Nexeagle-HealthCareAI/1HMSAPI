using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class UserAuth
    {
        [Key]
        public Guid UserAuthID { get; set; }
        public Guid UserID { get; set; }
        public int UserStatusId { get; set; }
        public UserStatus? UserStatus { get; set; }
        public string? HashedPassword { get; set; }
        public string? LoginMethod { get; set; }
        public string? Otp { get; set; }
        public DateTime? OtpSentDateTime { get; set; }
        public bool IsOtpUsed { get; set; } = false;
        public int FailedLoginAttempts { get; set; } = 0;
        public bool IsLocked { get; set; } = false;
        public string? LastLoginIP { get; set; }
        public DateTime? LastLoginTime { get; set; }
        public DateTime? PasswordSetAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public User User { get; set; } = null!;
        public DateTime? OtpExpireAt { get; set; }
    }
}
