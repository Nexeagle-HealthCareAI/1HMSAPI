using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Api.Common
{
    /// <summary>
    /// Gate for the public (Nexeagle-facing) controller. Reads the X-Api-Key header, hashes it,
    /// and looks up an active PublicApiClient row — the resolved HospitalId is stamped onto
    /// HttpContext.Items so the controller action can bind it onto the request, mirroring how
    /// staff-authenticated actions pull HospitalId from a JWT claim. A per-hospital key means a
    /// leaked key only ever exposes the one hospital it was issued for; the caller can never
    /// supply a different hospitalId to reach another tenant's data.
    /// Applied per-controller via [ServiceFilter(typeof(PublicApiKeyFilter))] — unlike
    /// HospitalAccessFilter this isn't registered globally, since only the public controller needs it.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class PublicApiKeyFilter : IAsyncActionFilter
    {
        public const string ApiKeyHeaderName = "X-Api-Key";
        public const string HospitalIdItemKey = "PublicApiHospitalId";

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
                context.Result = new ObjectResult(new { message = "Missing X-Api-Key header." })
                {
                    StatusCode = StatusCodes.Status401Unauthorized,
                };
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

            context.HttpContext.Items[HospitalIdItemKey] = client.HospitalId;

            await next();
        }
    }
}
