using System.ComponentModel.DataAnnotations;

namespace EasyHMSAPI.Domain.Entities
{
    public class Role
    {
        [Key]
        public Guid RoleID { get; set; }
        public Guid? HospitalID { get; set; }
        public string RoleName { get; set; } = null!;
        public string? Description { get; set; }
        public bool IsSystemDefined { get; set; } = false;
        public bool IsActive { get; set; } = true;
        public Guid? CreatedByUserID { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public Hospital? Hospital { get; set; }
        public User? CreatedByUser { get; set; }
        public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
        public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
        public ICollection<UserInvitation> UserInvitations { get; set; } = new List<UserInvitation>();
    }
}