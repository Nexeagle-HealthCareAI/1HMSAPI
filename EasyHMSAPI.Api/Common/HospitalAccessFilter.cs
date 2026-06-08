using EasyHMSAPI.Domain.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Api.Common
{
    /// <summary>
    /// Multi-tenant guard: when a request targets a specific hospital (hospitalId in route, query, or
    /// the bound request body), verifies the signed-in user is a member of that hospital
    /// (HospitalUsers). Fail-OPEN when no hospitalId is present (nothing tenant-scoped) and fail-CLOSED
    /// (403) when a hospitalId is present but the caller isn't a member. Opt out with
    /// [SkipHospitalAccessCheck] on identity/setup controllers.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class HospitalAccessFilter : IAsyncActionFilter
    {
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);
        private readonly AppDbContext _db;
        private readonly IMemoryCache _cache;

        public HospitalAccessFilter(AppDbContext db, IMemoryCache cache)
        {
            _db = db;
            _cache = cache;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (context.ActionDescriptor.EndpointMetadata.Any(m => m is SkipHospitalAccessCheckAttribute))
            {
                await next();
                return;
            }

            var userId = UserContextHelper.GetUserId(context.HttpContext.User);
            if (userId == null) { await next(); return; } // anonymous endpoints handle their own auth

            var hospitalId = ExtractHospitalId(context);
            if (hospitalId == null || hospitalId == Guid.Empty) { await next(); return; } // nothing to scope

            var key = $"hu:{userId}:{hospitalId}";
            bool isMember;
            if (_cache.TryGetValue(key, out bool cached))
            {
                isMember = cached;
            }
            else
            {
                isMember = await _db.HospitalUsers
                    .AsNoTracking()
                    .AnyAsync(hu => hu.UserID == userId.Value && hu.HospitalID == hospitalId.Value);
                _cache.Set(key, isMember, CacheTtl);
            }

            if (!isMember)
            {
                context.Result = new ObjectResult(new { message = "You don't have access to this hospital." })
                {
                    StatusCode = StatusCodes.Status403Forbidden,
                };
                return;
            }

            await next();
        }

        // First non-empty of: a Guid action arg named hospitalId, a bound body with a HospitalId
        // property, the route value, or the query string. Only the canonical "hospitalId" name.
        private static Guid? ExtractHospitalId(ActionExecutingContext context)
        {
            foreach (var (k, v) in context.ActionArguments)
            {
                if (v == null) continue;
                if (string.Equals(k, "hospitalId", StringComparison.OrdinalIgnoreCase) && v is Guid g && g != Guid.Empty)
                    return g;
            }

            foreach (var v in context.ActionArguments.Values)
            {
                if (v == null) continue;
                var t = v.GetType();
                if (t.IsPrimitive || t == typeof(string) || t == typeof(Guid) || t.IsEnum) continue;
                var prop = t.GetProperty("HospitalId") ?? t.GetProperty("HospitalID");
                if (prop == null) continue;
                var pv = prop.GetValue(v);
                if (pv is Guid pg && pg != Guid.Empty) return pg;
            }

            if (context.RouteData.Values.TryGetValue("hospitalId", out var routeVal)
                && Guid.TryParse(routeVal?.ToString(), out var rg) && rg != Guid.Empty)
                return rg;

            var q = context.HttpContext.Request.Query["hospitalId"].ToString();
            if (Guid.TryParse(q, out var qg) && qg != Guid.Empty) return qg;

            return null;
        }
    }
}
