using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class UserRole
    {
        public Guid UserID { get; set; }
        public Guid RoleID { get; set; }
        public User User { get; set; } = null!;
        public Role Role { get; set; } = null!;
    }
} 