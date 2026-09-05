namespace EasyHMSAPI.Application.Services.Interfaces
{
    public class UsageLimitResult
    {
        public bool Allowed { get; set; }
        public int UsedCount { get; set; }
        public int Limit { get; set; }
        // Populated only when Allowed is false -- ready to surface directly to the caller.
        public string? Message { get; set; }
    }

    // Gates + meters the pooled monthly free-tier quota (IPD admission, OPD appointment
    // confirm/walk-in, pathology order, pharmacy checkout) that replaced the old time-based trial.
    // Only hospitals still on the "Trial" subscription status are ever blocked -- an Active (paid)
    // hospital always gets Allowed=true (still metered, for reporting, just never denied).
    public interface IUsageLimitService
    {
        // Atomic check-and-increment: never returns Allowed=true without having already consumed
        // the unit, and never consumes a unit when returning Allowed=false. Call this as the LAST
        // gate immediately before the action's actual persistence, inside the same transaction
        // when the caller has one, so a later failure in that same transaction rolls the
        // consumed unit back too.
        Task<UsageLimitResult> TryConsumeAsync(Guid hospitalId, CancellationToken cancellationToken);

        // Read-only status (current usage + effective limit + whether this hospital is even
        // subject to the cap) -- for surfacing remaining quota in the UI and for Phase 2's
        // over-limit view-masking decision. Never mutates anything.
        Task<UsageLimitResult> GetStatusAsync(Guid hospitalId, CancellationToken cancellationToken);
    }
}
