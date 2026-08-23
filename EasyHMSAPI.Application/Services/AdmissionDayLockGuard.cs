using EasyHMSAPI.Domain.Entities;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>
    /// Checks whether a charge's ServiceDate would fall inside an already-closed, already-printed
    /// admission-day interim bill -- used when a visit's ServiceDate (Encounter.ServiceDate) isn't
    /// "now", so a freshly-posted charge could otherwise land in a period that's already final.
    /// </summary>
    public static class AdmissionDayLockGuard
    {
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
