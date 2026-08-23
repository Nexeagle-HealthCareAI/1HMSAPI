using EasyHMSAPI.Domain.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

// Lets PermissionAuthorizationFilterTests exercise ResolveGrantedPermissionsAsync directly
// without constructing a real ActionExecutingContext -- see that method's own doc comment.
[assembly: InternalsVisibleTo("EasyHMSAPI.UnitTests")]

namespace EasyHMSAPI.Api.Common
{
    /// <summary>
    /// Board-level guard: when an action carries [RequiresPermission(...)], verifies the
    /// signed-in user holds at least one of the given PermissionKeys (via
    /// UserRoles -> Role -> RolePermissions, IsAllowed = true). Fail-OPEN when unannotated
    /// (matches today's behavior exactly -- lets rollout happen controller-by-controller)
    /// and fail-CLOSED (403) when annotated but the caller lacks every listed key. Mirrors
    /// HospitalAccessFilter's shape (opt-in here instead of opt-out, IMemoryCache with the
    /// same short TTL) rather than introducing a new authorization mechanism.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class PermissionAuthorizationFilter : IAsyncActionFilter
    {
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);
        private readonly AppDbContext _db;
        private readonly IMemoryCache _cache;

        public PermissionAuthorizationFilter(AppDbContext db, IMemoryCache cache)
        {
            _db = db;
            _cache = cache;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var attr = context.ActionDescriptor.EndpointMetadata.OfType<RequiresPermissionAttribute>().FirstOrDefault();
            if (attr == null || attr.PermissionKeys.Length == 0) { await next(); return; }

            var userId = UserContextHelper.GetUserId(context.HttpContext.User);
            if (userId == null) { await next(); return; } // anonymous endpoints handle their own auth

            var granted = await ResolveGrantedPermissionsAsync(userId.Value);
            if (!attr.PermissionKeys.Any(k => granted.Contains(k)))
            {
                context.Result = new ObjectResult(new { message = "You don't have permission to access this resource." })
                {
                    StatusCode = StatusCodes.Status403Forbidden,
                };
                return;
            }

            await next();
        }

        // Internal (not private) so tests can exercise the DB/cache logic directly without
        // constructing a real ActionExecutingContext -- same reasoning as why
        // HospitalAccessFilter itself has never needed a filter-level unit test.
        internal async Task<HashSet<string>> ResolveGrantedPermissionsAsync(Guid userId)
        {
            var key = $"perm:{userId}";
            if (_cache.TryGetValue(key, out HashSet<string>? cached) && cached != null)
            {
                return cached;
            }

            var granted = await _db.UserRoles
                .AsNoTracking()
                .Where(ur => ur.UserID == userId)
                .SelectMany(ur => ur.Role.RolePermissions)
                .Where(rp => rp.IsAllowed)
                .Select(rp => rp.PermissionKey)
                .Distinct()
                .ToListAsync();

            var result = new HashSet<string>(granted, StringComparer.OrdinalIgnoreCase);
            _cache.Set(key, result, CacheTtl);
            return result;
        }
    }
}
