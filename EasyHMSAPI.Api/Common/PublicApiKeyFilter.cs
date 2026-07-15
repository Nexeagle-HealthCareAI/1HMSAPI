using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Api.Common
{
    /// <summary>
    /// Gate for the public (Nexeagle-facing) controller. The X-Api-Key header is optional — this
    /// is a generic "list publicly-listed doctors, let anyone book/review" platform surface, not
    /// confidential data, so an anonymous caller with no header is let through untracked. A caller
    /// that DOES send the header must present a valid, active PublicApiClient key (hashed lookup) —
    /// this is only for consumers who want their traffic identified/revocable later (e.g. a
    /// specific partner integration), never a requirement for basic access.
    /// Every action resolves its own HospitalId from the doctor/appointment being acted on
    /// (+ Hospital.IsPubliclyListed), never from the key itself, whether or not a key was sent.
    /// Applied per-controller via [ServiceFilter(typeof(PublicApiKeyFilter))] — unlike
    /// HospitalAccessFilter this isn't registered globally, since only the public controller needs it.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class PublicApiKeyFilter : IAsyncActionFilter
    {
        public const string ApiKeyHeaderName = "X-Api-Key";

        private readonly AppDbContext _db;

        public PublicApiKeyFilter(AppDbContext db)
        {
            _db = db;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (!context.HttpContext.Request.Headers.TryGetValue(ApiKeyHeaderName, out var apiKeyValues)
                || string.IsNullOrWhiteSpace(apiKeyValues.ToString()))
            {
                await next();
                return;
            }

            var apiKeyHash = ApiKeyHasher.Hash(apiKeyValues.ToString());
            var client = await _db.PublicApiClient
                .FirstOrDefaultAsync(c => c.ApiKeyHash == apiKeyHash && c.IsActive);

            if (client == null)
            {
                context.Result = new ObjectResult(new { message = "Invalid or inactive API key." })
                {
                    StatusCode = StatusCodes.Status401Unauthorized,
                };
                return;
            }

            client.LastUsedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            await next();
        }
    }
}
