namespace EasyHMSAPI.Domain.Entities
{
    public class RolePermission
    {
        public Guid RoleID { get; set; }
        public string PermissionKey { get; set; } = null!;
        public bool IsAllowed { get; set; } = true;
        public Role Role { get; set; } = null!;
        public Permission Permission { get; set; } = null!;
    }
} 