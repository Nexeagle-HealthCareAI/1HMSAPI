using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class RolePermission
    {
        public Guid RoleID { get; set; }
        public string PermissionKey { get; set; } = string.Empty;
        public Role Role { get; set; } = null!;
    }
}