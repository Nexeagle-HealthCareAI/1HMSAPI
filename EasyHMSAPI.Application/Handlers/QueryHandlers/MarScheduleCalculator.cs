using EasyHMSAPI.Data.Constants;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>
    /// Computes the set of scheduled dose slots (UTC datetimes) for one ClinicalOrderLine, given
    /// its Frequency code, DurationDays, and the order's OrderedAt. Pure/stateless — no DB access
    /// — so GetMarGridHandler can call it per line after loading orders+lines in bulk, and it's
    /// independently unit-testable. Slots are generated against IST ward-clock times (this
    /// codebase assumes IST hospital-wide, matching every other IST-formatting convention already
    /// in use, e.g. ClinicalOrderPanel.tsx's toIstDate/formatIstDateTime) but returned as UTC.
    /// </summary>
    public static class MarScheduleCalculator
    {
        private static readonly TimeSpan IstOffset = TimeSpan.FromHours(5.5);

        // Grace-period design: a nurse acting within +/-45 min of the exact slot time counts as
        // administering THAT slot (matched to it, not treated as a duplicate/new slot). Beyond
        // +45 min with no action, the slot is OVERDUE; beyond +3h with still no action, it's
        // MISSED. DUE covers the window from -15 min (slot is imminent) through +45 min. These
        // thresholds are deliberately generous for ward realities (nurse mid-round, one patient at
        // a time) vs. e.g. an ICU titration schedule — there is no per-hospital configuration
        // surface for this in Phase 4.
        public static readonly TimeSpan DueLeadTime = TimeSpan.FromMinutes(15);
        public static readonly TimeSpan MatchTolerance = TimeSpan.FromMinutes(45);
        public static readonly TimeSpan MissedThreshold = TimeSpan.FromHours(3);

        /// <summary>
        /// Returns every computed slot (UTC) for this line, bounded to [windowStartUtc,
        /// windowEndUtc] AND to the line's own DurationDays window from orderedAtUtc (whichever is
        /// narrower). Returns empty for SOS/PRN (ad-hoc only, never pre-scheduled) and for
        /// unrecognized/free-text Frequency values (pre-Phase-4 orders) — callers should treat
        /// those lines as "ad-hoc only".
        /// </summary>
        public static List<DateTime> ComputeSlots(
            string? frequency, DateTime orderedAtUtc, int? durationDays,
            DateTime windowStartUtc, DateTime windowEndUtc)
        {
            var freq = frequency?.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(freq) || freq == IpdConstants.MedicationFrequency.Sos)
                return new List<DateTime>();

            // Bound the far end by DurationDays from the order's first dose, if given.
            var lineEndUtc = durationDays.HasValue ? orderedAtUtc.AddDays(durationDays.Value) : (DateTime?)null;
            var effectiveEndUtc = lineEndUtc.HasValue && lineEndUtc.Value < windowEndUtc ? lineEndUtc.Value : windowEndUtc;
            if (effectiveEndUtc <= windowStartUtc || orderedAtUtc > effectiveEndUtc)
                return new List<DateTime>();

            var slots = new List<DateTime>();

            if (freq == IpdConstants.MedicationFrequency.Stat)
            {
                // One immediate slot at order time — included only if it falls in the window.
                if (orderedAtUtc >= windowStartUtc && orderedAtUtc <= effectiveEndUtc)
                    slots.Add(orderedAtUtc);
                return slots;
            }

            if (IpdConstants.MedicationFrequency.IntervalHours.TryGetValue(freq, out var hours))
            {
                // Rolling interval starting from the order's first-dose time, not a ward clock.
                var cursor = orderedAtUtc;
                while (cursor <= effectiveEndUtc)
                {
                    if (cursor >= windowStartUtc) slots.Add(cursor);
                    cursor = cursor.AddHours(hours);
                }
                return slots;
            }

            if (IpdConstants.MedicationFrequency.ClockTimes.TryGetValue(freq, out var clockTimes))
            {
                // Fixed ward clock times per IST calendar day, from the order's start day through
                // the effective end, inclusive of partial first/last days.
                var startIstDate = (orderedAtUtc + IstOffset).Date;
                var endIstDate = (effectiveEndUtc + IstOffset).Date;
                for (var day = startIstDate; day <= endIstDate; day = day.AddDays(1))
                {
                    foreach (var t in clockTimes)
                    {
                        var slotUtc = day + t - IstOffset;
                        if (slotUtc >= orderedAtUtc && slotUtc >= windowStartUtc && slotUtc <= effectiveEndUtc)
                            slots.Add(slotUtc);
                    }
                }
                return slots;
            }

            // Unrecognized/legacy free-text Frequency (pre-Phase-4 order) — no computed slots.
            return slots;
        }

        /// <summary>
        /// Derives a computed (unmatched) slot's display status from how far "now" is past its
        /// scheduled time. Callers must only invoke this when no administration row matched the
        /// slot — an actual administration's own ActionStatus is always used verbatim instead.
        /// </summary>
        public static string DeriveSlotStatus(DateTime scheduledForUtc, DateTime nowUtc)
        {
            var delta = nowUtc - scheduledForUtc;
            if (delta < -DueLeadTime) return IpdConstants.MarSlotStatus.Pending;
            if (delta <= MatchTolerance) return IpdConstants.MarSlotStatus.Due;
            if (delta <= MissedThreshold) return IpdConstants.MarSlotStatus.Overdue;
            return IpdConstants.MarSlotStatus.Missed;
        }
    }
}
