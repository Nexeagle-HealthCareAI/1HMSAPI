using System;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;

namespace EasyHMSAPI.UnitTests.TestUtils
{
    /// <summary>
    /// Seeds the hospital-membership + role-permission rows that CallerGuards.HasPermissionAtHospitalAsync
    /// reads, so HR handler tests can act as "a payroll manager at hospital X" (or "a plain member").
    /// </summary>
    public static class HrAuthSeed
    {
        /// <summary>
        /// A user who belongs to <paramref name="hospitalId"/> and holds <paramref name="permissionKeys"/>
        /// through a role owned by that hospital. With no keys the user is a plain member with no role.
        /// </summary>
        public static Guid SeedMember(AppDbContext context, Guid hospitalId, params string[] permissionKeys)
        {
            var userId = Guid.NewGuid();
            context.HospitalUsers.Add(new HospitalUser { HospitalUserID = Guid.NewGuid(), HospitalID = hospitalId, UserID = userId });

            if (permissionKeys.Length > 0)
            {
                var roleId = Guid.NewGuid();
                context.Roles.Add(new Role { RoleID = roleId, HospitalID = hospitalId, RoleName = "HR Manager" });
                foreach (var key in permissionKeys)
                {
                    context.RolePermissions.Add(new RolePermission { RoleID = roleId, PermissionKey = key, IsAllowed = true });
                }
                context.UserRoles.Add(new UserRole { UserID = userId, RoleID = roleId });
            }

            context.SaveChanges();
            return userId;
        }
    }
}
