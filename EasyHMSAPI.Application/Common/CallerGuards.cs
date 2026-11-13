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

        /// <summary>True if the two users are members of at least one common hospital.</summary>
        public static Task<bool> SharesHospitalAsync(AppDbContext context, Guid callerUserId, Guid targetUserId, CancellationToken cancellationToken)
        {
            if (callerUserId == Guid.Empty || targetUserId == Guid.Empty) return Task.FromResult(false);
            return context.HospitalUsers
                .Where(hu => hu.UserID == callerUserId)
                .AnyAsync(caller => context.HospitalUsers.Any(target =>
                    target.UserID == targetUserId && target.HospitalID == caller.HospitalID), cancellationToken);
        }

        /// <summary>
        /// Self-or-admin access to another user's own record (profile, picture, details). True when the
        /// caller IS the target, or holds <paramref name="permissionKey"/> (default admin_panel) at a hospital
        /// the target also belongs to. The caller id must come from the verified JWT, never the client.
        /// </summary>
        public static async Task<bool> CanAccessUserAsync(AppDbContext context, Guid? callerUserId, Guid targetUserId, CancellationToken cancellationToken, string permissionKey = "admin_panel")
        {
            if (callerUserId == null || callerUserId == Guid.Empty || targetUserId == Guid.Empty) return false;
            if (callerUserId.Value == targetUserId) return true;

            var sharedHospitalIds = await context.HospitalUsers
                .Where(hu => hu.UserID == callerUserId.Value)
                .Select(hu => hu.HospitalID)
                .Where(hid => context.HospitalUsers.Any(t => t.UserID == targetUserId && t.HospitalID == hid))
                .ToListAsync(cancellationToken);

            foreach (var hospitalId in sharedHospitalIds)
            {
                if (await HasPermissionAtHospitalAsync(context, callerUserId.Value, hospitalId, permissionKey, cancellationToken))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// True if the caller is a member of <paramref name="hospitalId"/> AND holds an Admin or AdminDoctor
        /// role that applies there (owned by that hospital, or a legacy global role with no hospital). A role
        /// owned by a DIFFERENT hospital does not count, so an admin of hospital A who is only a staff member
        /// of hospital B is not an administrator of B.
        /// </summary>
        public static async Task<bool> IsAdminAtHospitalAsync(AppDbContext context, Guid callerUserId, Guid hospitalId, CancellationToken cancellationToken)
        {
            if (callerUserId == Guid.Empty || hospitalId == Guid.Empty) return false;
            if (!await IsHospitalMemberAsync(context, callerUserId, hospitalId, cancellationToken)) return false;

            var roles = await context.UserRoles
                .Where(ur => ur.UserID == callerUserId && (ur.Role.HospitalID == null || ur.Role.HospitalID == hospitalId))
                .Select(ur => ur.Role.RoleName)
                .ToListAsync(cancellationToken);
            return roles.Any(r => !string.IsNullOrWhiteSpace(r) && AdminRoles.Contains(r.Trim().ToLowerInvariant()));
        }
    }
}
