using EasyHMSAPI.Domain.Context;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Services
{
    // Soft-cancels an ACCRUED consultant incentive when the charge that earned it is voided or
    // deleted -- preserves the audit trail (never hard-deletes the ledger row), matches this
    // codebase's established revoke-never-delete convention. Never touches a row already PAID
    // (settled) -- clawing back a paid-out incentive is a separate reconciliation concern.
    public static class ConsultantIncentiveHelper
    {
        public static async Task CancelForChargeAsync(
            AppDbContext context, Guid chargeEventId, string? cancelledBy, string cancelReason, CancellationToken cancellationToken)
        {
            var ledgerEntry = await context.ConsultantIncentiveLedger
                .Where(l => l.ChargeEventId == chargeEventId && l.StatusCode == "ACCRUED")
                .FirstOrDefaultAsync(cancellationToken);

            if (ledgerEntry == null)
                return;

            var now = DateTime.UtcNow;
            ledgerEntry.StatusCode = "CANCELLED";
            ledgerEntry.CancelledAt = now;
            ledgerEntry.CancelledBy = cancelledBy;
            ledgerEntry.CancelReason = cancelReason;
            ledgerEntry.UpdatedAt = now;
            ledgerEntry.UpdatedBy = cancelledBy;
        }
    }
}
