namespace EasyHMSAPI.Api.Common
{
    /// <summary>
    /// Decides whether a hospital's subscription status should block a write request. Extracted
    /// from HospitalAccessFilter so the decision is unit-testable without constructing a real
    /// ActionExecutingContext (same reasoning as PermissionAuthorizationFilter's internal
    /// ResolveGrantedPermissionsAsync) and, critically, so the null-safety here is covered by a
    /// test instead of living only inline in a filter nothing exercises directly.
    ///
    /// Null-safe by construction: a missing/unreadable status never blocks a write -- mirrors the
    /// filter's own "Trial" default when no subscription row exists at all (fail-open on
    /// uncertainty, fail-closed only on a genuinely known bad status). Before this, the filter
    /// called subStatus.Equals(...) directly on a string? sourced from an IMemoryCache read,
    /// which throws NullReferenceException if a null ever reaches that cache slot; because this
    /// runs as an action filter -- outside every controller's own try/catch -- that exception
    /// surfaces as a raw, unlogged 500 instead of a handled error response.
    /// </summary>
    public static class SubscriptionLockoutPolicy
    {
        private static readonly string[] LockedStatuses = { "Expired", "Blocked", "Rejected" };

        public static bool IsLockedOut(string? subscriptionStatus)
        {
            if (subscriptionStatus == null) return false;
            return LockedStatuses.Any(s => string.Equals(subscriptionStatus, s, StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsRejected(string? subscriptionStatus) =>
            string.Equals(subscriptionStatus, "Rejected", StringComparison.OrdinalIgnoreCase);
    }
}
