using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Api.Common
{
    /// <summary>
    /// Sign-in check for the /contributor endpoints. The Doctor Dekho server forwards the session token it holds in an
    /// HttpOnly cookie as the X-Contributor-Session header. Only its hash is looked up; the session must be neither revoked
    /// nor expired, and the person must not have been rejected since. On success the contributor id is available to the
    /// action through ContributorId(HttpContext). Applied per action with [ServiceFilter].
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class ContributorSessionFilter : IAsyncActionFilter
    {
        public const string HeaderName = "X-Contributor-Session";
        private const string ContributorKey = "HealthWiki.ContributorId";
        private const string SessionKey = "HealthWiki.SessionId";

        private readonly AppDbContext _context;

        public ContributorSessionFilter(AppDbContext context)
        {
            _context = context;
        }

        public static Guid? ContributorId(HttpContext http) => http.Items[ContributorKey] as Guid?;
        public static Guid? SessionId(HttpContext http) => http.Items[SessionKey] as Guid?;

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var token = context.HttpContext.Request.Headers[HeaderName].ToString();
            if (!string.IsNullOrWhiteSpace(token))
            {
                var hash = ContributorSecurity.HashToken(token.Trim());
                var now = DateTime.UtcNow;
                var session = await _context.ContributorSessions
                    .FirstOrDefaultAsync(s => s.TokenHash == hash && s.RevokedAt == null && s.ExpiresAt > now, context.HttpContext.RequestAborted);
                if (session != null)
                {
                    var active = await _context.HealthWikiContributors.AsNoTracking()
                        .AnyAsync(c => c.ContributorId == session.ContributorId && c.Status != HealthWikiContributor.StatusRejected, context.HttpContext.RequestAborted);
                    if (active)
                    {
                        // Recorded at most every few minutes, so a busy page does not write on every call.
                        if (session.LastSeenAt == null || now - session.LastSeenAt > TimeSpan.FromMinutes(5))
                        {
                            session.LastSeenAt = now;
                            await _context.SaveChangesAsync(context.HttpContext.RequestAborted);
                        }
                        context.HttpContext.Items[ContributorKey] = session.ContributorId;
                        context.HttpContext.Items[SessionKey] = session.SessionId;
                        await next();
                        return;
                    }
                }
            }

            context.Result = new ObjectResult(new { message = "Please sign in again." }) { StatusCode = StatusCodes.Status401Unauthorized };
        }
    }
}
