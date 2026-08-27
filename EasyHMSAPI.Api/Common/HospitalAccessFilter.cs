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
        // Shorter than CacheTtl: archiving is a deliberate, singular admin action (unlike a
        // subscription merely lapsing), so a prompt cutoff matters more than staleness tolerance.
        private static readonly TimeSpan ArchiveCacheTtl = TimeSpan.FromSeconds(15);
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

            // --- Archived Check --- (blocks BOTH reads and writes — the goal is hiding
            // everything about this hospital, not just stopping new writes, unlike the
            // subscription check below which deliberately still allows reads through)
            var archKey = $"arch:{hospitalId}";
            bool isArchived;
            if (_cache.TryGetValue(archKey, out bool cachedArchived))
            {
                isArchived = cachedArchived;
            }
            else
            {
                isArchived = await _db.Hospitals
                    .AsNoTracking()
                    .Where(h => h.HospitalID == hospitalId.Value)
                    .Select(h => h.IsArchived)
                    .FirstOrDefaultAsync();
                _cache.Set(archKey, isArchived, ArchiveCacheTtl);
            }

            if (isArchived)
            {
                context.Result = new ObjectResult(new
                {
                    message = "This hospital has been archived and is no longer accessible.",
                    hospitalArchived = true
                })
                {
                    StatusCode = StatusCodes.Status403Forbidden,
                };
                return;
            }

            // --- Subscription Check ---
            var subKey = $"sub:{hospitalId}";
            string? subStatus;
            if (_cache.TryGetValue(subKey, out string? cachedStatus))
            {
                subStatus = cachedStatus;
            }
            else
            {
                var sub = await _db.HospitalSubscriptions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.HospitalId == hospitalId.Value);

                subStatus = sub?.GetEffectiveStatus(DateTime.UtcNow) ?? "Trial"; // Default if missing
                _cache.Set(subKey, subStatus, CacheTtl);
            }

            // Read-only once expired/blocked/rejected, not a full lockout: GET (and HEAD) requests
            // — viewing the appointment board, patient records, billing history, etc. — pass through
            // regardless of subscription status. Only requests that mutate something (POST/PUT/PATCH/
            // DELETE) get stopped here; the frontend mirrors this by disabling write actions while
            // leaving navigation/viewing enabled (see MainLayout.tsx / AppointmentDashboard.tsx).
            var isWriteRequest = !HttpMethods.IsGet(context.HttpContext.Request.Method)
                && !HttpMethods.IsHead(context.HttpContext.Request.Method)
                && !HttpMethods.IsOptions(context.HttpContext.Request.Method);

            if (isWriteRequest && SubscriptionLockoutPolicy.IsLockedOut(subStatus))
            {
                // Check if user is Admin or AdminDoctor
                var roles = await _db.UserRoles
                    .AsNoTracking()
                    .Include(ur => ur.Role)
                    .Where(ur => ur.UserID == userId.Value && (ur.Role.HospitalID == null || ur.Role.HospitalID == hospitalId.Value))
                    .Select(ur => ur.Role.RoleName)
                    .ToListAsync();

                bool isAdmin = roles.Contains("Admin") || roles.Contains("AdminDoctor");
                var isRejected = SubscriptionLockoutPolicy.IsRejected(subStatus);

                if (isAdmin)
                {
                    context.Result = new ObjectResult(new {
                        message = isRejected
                            ? "Your last payment submission was rejected. Please review the reason and resubmit."
                            : "Your hospital subscription has expired. Please renew to continue.",
                        subscriptionExpired = true
                    })
                    {
                        StatusCode = StatusCodes.Status402PaymentRequired,
                    };
                }
                else
                {
                    context.Result = new ObjectResult(new {
                        message = "Hospital subscription is inactive. Please contact your administrator.",
                        subscriptionExpired = true
                    })
                    {
                        StatusCode = StatusCodes.Status403Forbidden,
                    };
                }
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
