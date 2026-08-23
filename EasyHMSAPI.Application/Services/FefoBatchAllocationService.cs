using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Services
{
    public class BatchAllocation
    {
        public Batch Batch { get; set; } = null!;
        public decimal AllocatedQty { get; set; }
    }

    // First-Expiry-First-Out batch selection — the one place this logic lives, reused by both the
    // FEFO picker query (frontend dropdown) and RecordInventoryMovement's auto-FEFO issue path.
    // Allocates across multiple batches if a single batch cannot fulfill the requested quantity.
    public static class FefoBatchAllocationService
    {
        public static async Task<List<BatchAllocation>> AllocateAsync(
            AppDbContext context, Guid hospitalId, Guid inventoryItemId, Guid storeId, decimal qty, CancellationToken cancellationToken)
        {
            var today = DateTime.UtcNow.Date;

            var eligibleBatches = await context.Batch
                .Where(b => b.HospitalId == hospitalId
                         && b.InventoryItemId == inventoryItemId
                         && b.StoreId == storeId
                         && b.Status == "ACTIVE"
                         && b.RemainingQty > 0
                         && (b.ExpiryDate == null || b.ExpiryDate >= today))
                .OrderBy(b => b.ExpiryDate ?? DateTime.MaxValue)
                .ThenBy(b => b.CreatedAt)
                .ToListAsync(cancellationToken);

            var allocations = new List<BatchAllocation>();
            decimal remainingToAllocate = qty;

            foreach (var batch in eligibleBatches)
            {
                if (remainingToAllocate <= 0) break;

                var allocationQty = Math.Min(batch.RemainingQty, remainingToAllocate);
                allocations.Add(new BatchAllocation { Batch = batch, AllocatedQty = allocationQty });
                remainingToAllocate -= allocationQty;
            }

            if (remainingToAllocate > 0)
            {
                return new List<BatchAllocation>();
            }

            return allocations;
        }
    }
}
