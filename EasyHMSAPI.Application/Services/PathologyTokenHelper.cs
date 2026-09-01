using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;

namespace EasyHMSAPI.Application.Services
{
    // Allocates PathologyOrder.TokenNumber -- a daily, per-hospital sequential counter (resets
    // every day) printed on a thermal receipt for the patient. Mirrors
    // AppointmentBookingHelpers.AllocateTokenWithLockingAsync's retry-on-collision shape, scoped to
    // the hospital only since pathology has no per-doctor queue concept.
    public static class PathologyTokenHelper
    {
        public static async Task<int> AllocateTokenWithLockingAsync(
            AppDbContext context,
            Guid hospitalId,
            DateTime orderDate,
            CancellationToken cancellationToken)
        {
            var tokenDate = orderDate.Date;
            const int maxAttempts = 5;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    var queue = await context.PathologyTokenQueue
                        .FirstOrDefaultAsync(q => q.HospitalId == hospitalId && q.TokenDate == tokenDate, cancellationToken);

                    int tokenNumber;
                    var now = DateTime.UtcNow;
                    if (queue == null)
                    {
                        queue = new PathologyTokenQueue
                        {
                            HospitalId = hospitalId,
                            TokenDate = tokenDate,
                            NextTokenNo = 2,
                            UpdatedAt = now,
                        };
                        context.PathologyTokenQueue.Add(queue);
                        tokenNumber = 1;
                    }
                    else
                    {
                        tokenNumber = queue.NextTokenNo;
                        queue.NextTokenNo++;
                        queue.UpdatedAt = now;
                    }

                    await context.SaveChangesAsync(cancellationToken);
                    return tokenNumber;
                }
                catch (DbUpdateException) when (attempt < maxAttempts)
                {
                    // Another concurrent order claimed this slot first -- either a PK collision
                    // creating a brand-new PathologyTokenQueue row, or a RowVersion mismatch
                    // updating an existing one. Discard whatever this attempt tracked and retry
                    // against a fresh read.
                    foreach (var entry in context.ChangeTracker.Entries()
                        .Where(e => e.Entity is PathologyTokenQueue)
                        .ToList())
                    {
                        entry.State = EntityState.Detached;
                    }
                }
            }

            throw new InvalidOperationException("Could not allocate a pathology token after retries.");
        }
    }
}
