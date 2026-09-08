using System.Diagnostics.CodeAnalysis;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Services.Implementations
{
    public class UsageLimitService : IUsageLimitService
    {
        private const int FallbackLimit = 100;
        private const string GlobalLimitSettingKey = "FreeTierMonthlyLimit";

        private readonly AppDbContext _context;

        public UsageLimitService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<UsageLimitResult> TryConsumeAsync(Guid hospitalId, CancellationToken cancellationToken)
        {
            var isGated = await IsGatedAsync(hospitalId, cancellationToken);
            if (!isGated)
                return new UsageLimitResult { Allowed = true, UsedCount = 0, Limit = int.MaxValue };

            var yearMonth = DateTime.UtcNow.ToString("yyyy-MM");
            var limit = await ResolveLimitAsync(hospitalId, cancellationToken);

            // Raw SQL, not EF Add/SaveChanges: this must be a single atomic "increment only if
            // still under the limit" operation to be race-safe under concurrent requests from the
            // same hospital -- same UPDLOCK/HOLDLOCK row-locking convention
            // InventoryCommandHandlers' handlers already use for exactly this class of problem.
            // ExecuteSqlRawAsync's return value (rows affected) IS the success signal, so no
            // separate read-then-write round trip is needed.
            var updated = await _context.Database.ExecuteSqlRawAsync(
                "UPDATE dbo.HospitalMonthlyUsage WITH (UPDLOCK, HOLDLOCK) SET UsedCount = UsedCount + 1, UpdatedAt = SYSUTCDATETIME() WHERE HospitalId = {0} AND YearMonth = {1} AND UsedCount < {2}",
                cancellationToken, hospitalId, yearMonth, limit);

            if (updated == 0)
            {
                // Either no row yet this month (first-ever action) or the limit is already reached.
                // WHERE NOT EXISTS ... WITH (UPDLOCK, HOLDLOCK) closes the same race for the
                // first-insert-of-the-month case two concurrent requests could otherwise both hit.
                var inserted = await _context.Database.ExecuteSqlRawAsync(
                    "INSERT INTO dbo.HospitalMonthlyUsage (HospitalId, YearMonth, UsedCount, UpdatedAt) " +
                    "SELECT {0}, {1}, 1, SYSUTCDATETIME() " +
                    "WHERE NOT EXISTS (SELECT 1 FROM dbo.HospitalMonthlyUsage WITH (UPDLOCK, HOLDLOCK) WHERE HospitalId = {0} AND YearMonth = {1}) AND {2} > 0",
                    cancellationToken, hospitalId, yearMonth, limit);

                if (inserted == 0)
                {
                    var used = await GetUsedCountAsync(hospitalId, yearMonth, cancellationToken);
                    return new UsageLimitResult
                    {
                        Allowed = false,
                        UsedCount = used,
                        Limit = limit,
                        Message = $"Free monthly limit of {limit} patient management actions reached. Upgrade your plan to continue, or wait until next month.",
                    };
                }
            }

            var finalUsed = await GetUsedCountAsync(hospitalId, yearMonth, cancellationToken);
            return new UsageLimitResult { Allowed = true, UsedCount = finalUsed, Limit = limit };
        }

        public async Task<UsageLimitResult> GetStatusAsync(Guid hospitalId, CancellationToken cancellationToken)
        {
            var isGated = await IsGatedAsync(hospitalId, cancellationToken);
            if (!isGated)
                return new UsageLimitResult { Allowed = true, UsedCount = 0, Limit = int.MaxValue };

            var yearMonth = DateTime.UtcNow.ToString("yyyy-MM");
            var limit = await ResolveLimitAsync(hospitalId, cancellationToken);
            var used = await GetUsedCountAsync(hospitalId, yearMonth, cancellationToken);

            return new UsageLimitResult
            {
                Allowed = used < limit,
                UsedCount = used,
                Limit = limit,
                Message = used < limit ? null : $"Free monthly limit of {limit} patient management actions reached. Upgrade your plan to continue, or wait until next month.",
            };
        }

        // Only a hospital still on the free ("Trial") tier is subject to the cap at all -- a paid
        // (Active) plan, or a hospital with no subscription row at all yet (defaults to "Trial" the
        // same way HospitalAccessFilter does), is gated; anything else (Blocked/Rejected are
        // already fully locked out by HospitalAccessFilter regardless of this check) is not.
        private async Task<bool> IsGatedAsync(Guid hospitalId, CancellationToken cancellationToken)
        {
            var status = await _context.HospitalSubscriptions
                .AsNoTracking()
                .Where(s => s.HospitalId == hospitalId)
                .Select(s => s.Status)
                .FirstOrDefaultAsync(cancellationToken);

            return string.IsNullOrWhiteSpace(status) || string.Equals(status, "Trial", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<int> ResolveLimitAsync(Guid hospitalId, CancellationToken cancellationToken)
        {
            var overrideLimit = await _context.HospitalFreeTierLimit
                .AsNoTracking()
                .Where(o => o.HospitalId == hospitalId)
                .Select(o => (int?)o.MonthlyLimit)
                .FirstOrDefaultAsync(cancellationToken);
            if (overrideLimit.HasValue)
                return overrideLimit.Value;

            var globalValue = await _context.PlatformSetting
                .AsNoTracking()
                .Where(s => s.SettingKey == GlobalLimitSettingKey)
                .Select(s => s.SettingValue)
                .FirstOrDefaultAsync(cancellationToken);

            return int.TryParse(globalValue, out var parsed) && parsed > 0 ? parsed : FallbackLimit;
        }

        private async Task<int> GetUsedCountAsync(Guid hospitalId, string yearMonth, CancellationToken cancellationToken)
        {
            return await _context.HospitalMonthlyUsage
                .AsNoTracking()
                .Where(u => u.HospitalId == hospitalId && u.YearMonth == yearMonth)
                .Select(u => u.UsedCount)
                .FirstOrDefaultAsync(cancellationToken);
        }
    }
}
