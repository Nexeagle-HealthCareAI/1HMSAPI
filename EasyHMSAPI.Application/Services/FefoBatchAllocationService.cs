using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Services
{
    // First-Expiry-First-Out batch selection — the one place this logic lives, reused by both the
    // FEFO picker query (frontend dropdown) and RecordInventoryMovement's auto-FEFO issue path.
    // V1 picks a SINGLE batch with enough RemainingQty rather than splitting across batches — if no
    // one batch covers the requested qty, the caller must issue a smaller qty or receive more stock.
    public static class FefoBatchAllocationService
    {
        public static async Task<Batch?> AllocateAsync(
            AppDbContext context, Guid hospitalId, Guid inventoryItemId, Guid storeId, decimal qty, CancellationToken cancellationToken)
        {
            var today = DateTime.UtcNow.Date;

            return await context.Batch
                .Where(b => b.HospitalId == hospitalId
                         && b.InventoryItemId == inventoryItemId
                         && b.StoreId == storeId
                         && b.Status == "ACTIVE"
                         && b.RemainingQty >= qty
                         && (b.ExpiryDate == null || b.ExpiryDate >= today))
                .OrderBy(b => b.ExpiryDate ?? DateTime.MaxValue)
                .ThenBy(b => b.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
        }
    }
}
