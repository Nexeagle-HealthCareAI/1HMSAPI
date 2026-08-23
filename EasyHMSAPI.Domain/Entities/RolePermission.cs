using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Domain.Entities
{
    [ExcludeFromCodeCoverage]
    public class RolePermission
    {
        public Guid RoleID { get; set; }
        public string PermissionKey { get; set; } = string.Empty;
        // Column has existed since the table was created (DF_RolePerm_Allowed DEFAULT(1))
        // but was never mapped on this entity until now -- see PermissionAuthorizationFilter
        // and UserPermissionsHandler, both of which need to respect an explicitly-revoked
        // (IsAllowed = 0) grant rather than treating every RolePermissions row as active.
        public bool IsAllowed { get; set; } = true;
        public Role Role { get; set; } = null!;
    }
}