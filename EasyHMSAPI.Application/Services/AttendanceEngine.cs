using System;
using System.Collections.Generic;
using System.Linq;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>A shift an employee is rostered on for a given date. Times are hospital wall-clock.</summary>
    /// <param name="RosterDate">The date the shift STARTS (a night shift belongs to the day it begins).</param>
    public sealed record RosteredShift(DateOnly RosterDate, string ShiftCode, TimeOnly Start, TimeOnly End, int GracePeriodMinutes)
    {
        public DateTime StartsAt => RosterDate.ToDateTime(Start);

        /// <summary>End is on the next day when the shift crosses midnight (End is not after Start).</summary>
        public DateTime EndsAt
        {
            get
            {
                var end = RosterDate.ToDateTime(End);
                return End <= Start ? end.AddDays(1) : end;
            }
        }

        public decimal ShiftHours => (decimal)(EndsAt - StartsAt).TotalHours;
    }

    /// <summary>One employee-day of attendance derived from raw scans.</summary>
    /// <param name="WorkDate">The shift's start date (or the calendar date of the scans when unscheduled).</param>
    /// <param name="Unscheduled">Scans exist but no shift was rostered for them.</param>
    public sealed record AttendanceDay(
        DateOnly WorkDate,
        DateTime PunchIn,
        DateTime? PunchOut,
        decimal? TotalHours,
        decimal OvertimeHours,
        string Status,
        bool Unscheduled,
        string? ShiftCode);

    public sealed class AttendanceEngineOptions
    {
        /// <summary>A repeat scan this soon after the previous kept one is a double-tap, not a new event.</summary>
        public TimeSpan Debounce { get; init; } = TimeSpan.FromMinutes(2);

        /// <summary>How long BEFORE a shift starts a scan still counts towards that shift.</summary>
        public TimeSpan EarlyWindow { get; init; } = TimeSpan.FromHours(3);

        /// <summary>How long AFTER a shift ends a scan still counts towards that shift (handover, staying late).</summary>
        public TimeSpan LateWindow { get; init; } = TimeSpan.FromHours(4);

        /// <summary>A later scan closer than this to the first is not treated as clocking out.</summary>
        public TimeSpan MinWorkSpan { get; init; } = TimeSpan.FromMinutes(30);

        /// <summary>Hours after which overtime starts when there is no rostered shift to measure against.</summary>
        public decimal DefaultStandardHours { get; init; } = 9m;
    }

    /// <summary>
    /// Turns a person's raw device scans into attendance days. Pure logic (no database) so the rules
    /// that decide people's pay are easy to test.
    ///
    /// Why in/out is INFERRED from scan order rather than read from the device: ZKTeco terminals send a
    /// "state" (check-in / check-out / break...) that depends on how each device's keys are set up, and
    /// many sites leave them on auto, so it is unreliable. The first scan of a shift is the IN, the last is
    /// the OUT, and anything between (breaks, double-taps) is ignored for hours.
    ///
    /// Scans are grouped by the SHIFT they fall in, not by calendar date, so a night shift that runs
    /// 20:00 -> 08:00 stays one attendance day instead of splitting into a lone IN and a lone OUT.
    /// </summary>
    public static class AttendanceEngine
    {
        public static IReadOnlyList<AttendanceDay> Compute(
            IEnumerable<DateTime> scanTimes,
            IEnumerable<RosteredShift> shifts,
            AttendanceEngineOptions? options = null)
        {
            options ??= new AttendanceEngineOptions();
            var scans = Debounce(scanTimes.OrderBy(t => t), options.Debounce);
            var rostered = shifts.OrderBy(s => s.StartsAt).ThenBy(s => s.ShiftCode).ToList();

            // Group scans by the shift they belong to (null = not inside any rostered shift's window).
            var byShift = new Dictionary<RosteredShift, List<DateTime>>();
            var unscheduledByDate = new SortedDictionary<DateOnly, List<DateTime>>();

            foreach (var scan in scans)
            {
                var shift = FindShift(scan, rostered, options);
                if (shift != null)
                {
                    if (!byShift.TryGetValue(shift, out var list)) byShift[shift] = list = new List<DateTime>();
                    list.Add(scan);
                }
                else
                {
                    var date = DateOnly.FromDateTime(scan);
                    if (!unscheduledByDate.TryGetValue(date, out var list)) unscheduledByDate[date] = list = new List<DateTime>();
                    list.Add(scan);
                }
            }

            var days = new List<AttendanceDay>();
            foreach (var (shift, group) in byShift) days.Add(BuildDay(shift.RosterDate, group, shift, options));
            foreach (var (date, group) in unscheduledByDate) days.Add(BuildDay(date, group, null, options));

            return days.OrderBy(d => d.WorkDate).ThenBy(d => d.PunchIn).ToList();
        }

        private static List<DateTime> Debounce(IEnumerable<DateTime> sorted, TimeSpan window)
        {
            var kept = new List<DateTime>();
            foreach (var t in sorted)
            {
                if (kept.Count == 0 || t - kept[^1] >= window) kept.Add(t);
            }
            return kept;
        }

        /// <summary>
        /// The rostered shift whose window contains the scan. When windows overlap (back-to-back shifts,
        /// a night shift followed by an early morning one) the nearest shift wins, and a scan exactly on a
        /// boundary goes to the earlier shift (it is that shift's clock-out).
        /// </summary>
        private static RosteredShift? FindShift(DateTime scan, List<RosteredShift> shifts, AttendanceEngineOptions options)
        {
            RosteredShift? best = null;
            TimeSpan bestDistance = TimeSpan.MaxValue;

            foreach (var shift in shifts)
            {
                if (scan < shift.StartsAt - options.EarlyWindow || scan > shift.EndsAt + options.LateWindow) continue;

                var distance = scan < shift.StartsAt ? shift.StartsAt - scan
                             : scan > shift.EndsAt ? scan - shift.EndsAt
                             : TimeSpan.Zero;

                if (distance < bestDistance)
                {
                    best = shift;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private static AttendanceDay BuildDay(DateOnly workDate, List<DateTime> scans, RosteredShift? shift, AttendanceEngineOptions options)
        {
            var punchIn = scans[0];
            var last = scans[^1];
            DateTime? punchOut = scans.Count >= 2 && last - punchIn >= options.MinWorkSpan ? last : null;

            decimal? total = punchOut.HasValue
                ? Math.Round((decimal)(punchOut.Value - punchIn).TotalHours, 2)
                : null;

            var standardHours = shift?.ShiftHours ?? options.DefaultStandardHours;
            var overtime = total.HasValue ? Math.Max(0m, Math.Round(total.Value - standardHours, 2)) : 0m;

            var late = shift != null && punchIn > shift.StartsAt.AddMinutes(shift.GracePeriodMinutes);

            return new AttendanceDay(
                WorkDate: workDate,
                PunchIn: punchIn,
                PunchOut: punchOut,
                TotalHours: total,
                OvertimeHours: overtime,
                Status: late ? "LATE" : "PRESENT",
                Unscheduled: shift == null,
                ShiftCode: shift?.ShiftCode);
        }
    }
}
