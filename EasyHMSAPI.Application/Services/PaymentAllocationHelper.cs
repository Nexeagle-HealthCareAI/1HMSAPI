using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Services
{
    // Breaks a payment→invoice BillingPaymentAllocation down into which specific charges the
    // money actually covered (BillingPaymentAllocationCharge), oldest charge first. This is what
    // lets cancelling ONE charge on a partially-paid, multi-service invoice reverse just that
    // charge's share of a payment instead of being blocked outright (QA sweep items 16/18).
    public static class PaymentAllocationHelper
    {
        public static async Task DistributeToChargesAsync(
            AppDbContext context, Guid invoiceId, Guid allocationId, decimal amount, string? createdBy, CancellationToken cancellationToken)
        {
            if (amount <= 0) return;

            var linkedChargeIds = await context.BillingInvoiceChargeEvent
                .Where(bice => bice.InvoiceId == invoiceId)
                .Select(bice => bice.ChargeEventId)
                .ToListAsync(cancellationToken);
            if (linkedChargeIds.Count == 0) return;

            var activeCharges = await context.BillingChargeEvent
                .Where(ce => linkedChargeIds.Contains(ce.ChargeEventId) && ce.StatusCode != BillingConstants.ChargeEventStatus.Void)
                .OrderBy(ce => ce.ServiceDate).ThenBy(ce => ce.CreatedAt)
                .ToListAsync(cancellationToken);
            if (activeCharges.Count == 0) return;

            var paidSoFarByCharge = await context.BillingPaymentAllocationCharge
                .Where(ac => linkedChargeIds.Contains(ac.ChargeEventId))
                .GroupBy(ac => ac.ChargeEventId)
                .Select(g => new { ChargeEventId = g.Key, Total = g.Sum(x => x.Amount) })
                .ToDictionaryAsync(x => x.ChargeEventId, x => x.Total, cancellationToken);

            var now = DateTime.UtcNow;
            var remaining = amount;
            foreach (var charge in activeCharges)
            {
                if (remaining <= 0) break;
                var alreadyPaid = paidSoFarByCharge.TryGetValue(charge.ChargeEventId, out var p) ? p : 0m;
                var outstanding = charge.NetAmount - alreadyPaid;
                if (outstanding <= 0) continue;

                var toApply = Math.Min(outstanding, remaining);
                context.BillingPaymentAllocationCharge.Add(new BillingPaymentAllocationCharge
                {
                    AllocationChargeId = Guid.NewGuid(),
                    AllocationId = allocationId,
                    ChargeEventId = charge.ChargeEventId,
                    Amount = toApply,
                    CreatedAt = now,
                    CreatedBy = createdBy,
                });
                remaining -= toApply;
            }
        }
    }
}
