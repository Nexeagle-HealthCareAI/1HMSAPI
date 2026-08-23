using EasyHMSAPI.Domain.Entities;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>
    /// Validates an optional caller-supplied date (charge ServiceDate / invoice InvoiceDate)
    /// against the "backdated billing" rules: never in the future, and a reason required whenever
    /// the date is actually in the past. Pure and dependency-free so both AddChargeEventHandler and
    /// CreateDraftInvoiceHandler share one tested implementation instead of duplicating the checks.
    /// </summary>
    public static class BillingBackdateGuard
    {
        public readonly record struct Result(bool Success, string? Error, DateTime EffectiveDate, bool IsBackdated);

        /// <summary>requestedDate == null means "use now" -- the unchanged, pre-backdating behavior.</summary>
        public static Result ValidateDate(DateTime? requestedDate, string? reason, DateTime nowUtc)
        {
            if (requestedDate is null)
                return new Result(true, null, nowUtc, false);

            if (requestedDate.Value > nowUtc)
                return new Result(false, "Date cannot be in the future.", nowUtc, false);

            var isBackdated = requestedDate.Value.Date < nowUtc.Date;
            if (isBackdated && string.IsNullOrWhiteSpace(reason))
                return new Result(false, "A reason is required to backdate this entry.", nowUtc, false);

            return new Result(true, null, requestedDate.Value, isBackdated);
        }

        /// <summary>
        /// closedDays: an encounter's AdmissionDayBill rows already CLOSED. Returns the earliest
        /// closed day (the one that must be reopened) when serviceDate falls before the latest
        /// closed day's window ends -- i.e. it would retroactively belong to an already-printed
        /// interim bill. Returns null when there's nothing to reopen (no closed days, or the date
        /// is on/after the latest closed window).
        /// </summary>
        public static AdmissionDayBill? FindBlockingClosedDay(IEnumerable<AdmissionDayBill> closedDays, DateTime serviceDate)
        {
            var list = closedDays as IList<AdmissionDayBill> ?? closedDays.ToList();
            if (list.Count == 0) return null;

            var latestToUtc = list.Max(d => d.ToUtc);
            if (serviceDate >= latestToUtc) return null;

            return list.OrderBy(d => d.DayNumber).First();
        }
    }
}
