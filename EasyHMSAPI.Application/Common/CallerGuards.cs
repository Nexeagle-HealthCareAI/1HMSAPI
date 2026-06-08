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
