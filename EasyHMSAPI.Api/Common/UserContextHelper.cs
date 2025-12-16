using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Domain.Context;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EasyHMSAPI.Api.Common
{
    public static class UserContextHelper
    {
        public static Guid? GetUserId(ClaimsPrincipal? user)
        {
            var idValue = user?.FindFirst("userId")?.Value;
            if (Guid.TryParse(idValue, out var id) && id != Guid.Empty)
            {
                return id;
            }
            return null;
        }

        public static async Task<string?> GetCurrentUserFullNameAsync(HttpContext httpContext, CancellationToken cancellationToken = default)
        {
            if (httpContext == null) return null;
            var userId = GetUserId(httpContext.User);
            if (userId == null) return null;

            var db = httpContext.RequestServices.GetRequiredService<AppDbContext>();

            var fullName = await db.UserProfiles
                .Where(up => up.UserID == userId.Value)
                .OrderByDescending(up => up.UpdatedAt)
                .Select(up => up.FullName)
                .FirstOrDefaultAsync(cancellationToken);

            return string.IsNullOrWhiteSpace(fullName) ? null : fullName;
        }
    }
}
