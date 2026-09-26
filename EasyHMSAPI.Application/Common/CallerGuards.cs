using EasyHMSAPI.Domain.Context;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Common
{
    /// <summary>
    /// Shared authorization checks for command handlers. The API authorizes by hospital membership
    /// (see HospitalAccessFilter); sensitive admin actions additionally require an admin role.
    /// </summary>
    public static class CallerGuards
    {
        private static readonly string[] AdminRoles = { "admin", "admindoctor" };

        /// <summary>True if the caller is a member of the hospital (HospitalUsers).</summary>
        public static Task<bool> IsHospitalMemberAsync(AppDbContext context, Guid callerUserId, Guid hospitalId, CancellationToken cancellationToken)
            => context.HospitalUsers.AnyAsync(hu => hu.UserID == callerUserId && hu.HospitalID == hospitalId, cancellationToken);

        /// <summary>
        /// True if the caller belongs to the hospital AND holds <paramref name="permissionKey"/> through a
        /// role that applies there (owned by that hospital, or a global role with no hospital).
        /// [RequiresPermission] alone is not enough for endpoints that take a record ID instead of a
        /// hospitalId: it checks the caller's roles across every hospital, and HospitalAccessFilter only
        /// guards requests that carry a hospitalId. Use this against the hospital that OWNS the record
        /// so a manager at hospital B cannot act on hospital A's data by ID.
        /// </summary>
        public static async Task<bool> HasPermissionAtHospitalAsync(AppDbContext context, Guid callerUserId, Guid hospitalId, string permissionKey, CancellationToken cancellationToken)
        {
            if (callerUserId == Guid.Empty || hospitalId == Guid.Empty) return false;
            if (!await IsHospitalMemberAsync(context, callerUserId, hospitalId, cancellationToken)) return false;

            return await context.UserRoles.AnyAsync(ur => ur.UserID == callerUserId
                && (ur.Role.HospitalID == null || ur.Role.HospitalID == hospitalId)
                && ur.Role.RolePermissions.Any(p => p.PermissionKey == permissionKey && p.IsAllowed), cancellationToken);
        }

        /// <summary>True if the caller holds an Admin or AdminDoctor role.</summary>
        public static async Task<bool> IsAdminAsync(AppDbContext context, Guid callerUserId, CancellationToken cancellationToken)
        {
            var roles = await context.UserRoles
                .Where(ur => ur.UserID == callerUserId)
                .Join(context.Roles, ur => ur.RoleID, r => r.RoleID, (ur, r) => r.RoleName)
                .ToListAsync(cancellationToken);

            return roles.Any(r => !string.IsNullOrWhiteSpace(r) && AdminRoles.Contains(r.Trim().ToLowerInvariant()));
        }
    }
}
