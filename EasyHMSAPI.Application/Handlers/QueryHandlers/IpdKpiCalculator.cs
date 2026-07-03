namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>
    /// Pure/stateless KPI math — no DB access, fed already-fetched rows by GetIpdKpiDashboardHandler.
    /// Mirrors MarScheduleCalculator/ApacheIIScoreCalculator's shape: independently unit-testable,
    /// the single source of truth for how raw admission/bed rows become the 5 dashboard metrics.
    /// </summary>
    public static class IpdKpiCalculator
    {
        public record BedSpan(Guid BedId, DateTime AssignedAt, DateTime? ReleasedAt);

        /// <summary>
        /// Daily BOR series across [fromDateUtc.Date, toDateUtc.Date] inclusive. A bed counts as
        /// occupied on day D if AssignedAt falls before the end of D and it wasn't released before
        /// the start of D. Denominator is the CURRENT active bed count (historical bed-count
        /// changes aren't tracked) — a stated limitation, not a bug.
        /// </summary>
        public static List<(DateTime Day, decimal BorPercent)> ComputeBorSeries(
            IReadOnlyList<BedSpan> spans, int totalActiveBeds, DateTime fromDateUtc, DateTime toDateUtc)
        {
            var result = new List<(DateTime, decimal)>();
            for (var day = fromDateUtc.Date; day <= toDateUtc.Date; day = day.AddDays(1))
            {
                if (totalActiveBeds <= 0)
                {
                    result.Add((day, 0m));
                    continue;
                }
                var dayStart = day;
                var dayEnd = day.AddDays(1);
                var occupied = spans
                    .Where(s => s.AssignedAt < dayEnd && (s.ReleasedAt == null || s.ReleasedAt > dayStart))
                    .Select(s => s.BedId)
                    .Distinct()
                    .Count();
                result.Add((day, Math.Round((decimal)occupied / totalActiveBeds * 100m, 1)));
            }
            return result;
        }

        /// <summary>Average length of stay in days, plus a weekly trend bucketed by DischargedAt's ISO week (Monday start).</summary>
        public static (decimal AverageDays, List<(DateTime WeekStart, decimal AvgDays)> Trend) ComputeAlos(
            IReadOnlyList<(DateTime AdmittedAt, DateTime DischargedAt)> admissions)
        {
            if (admissions.Count == 0) return (0m, new());

            var avg = Math.Round(admissions.Average(a => (decimal)(a.DischargedAt - a.AdmittedAt).TotalDays), 1);

            var trend = admissions
                .GroupBy(a => StartOfWeek(a.DischargedAt))
                .OrderBy(g => g.Key)
                .Select(g => (WeekStart: g.Key, AvgDays: Math.Round(g.Average(a => (decimal)(a.DischargedAt - a.AdmittedAt).TotalDays), 1)))
                .ToList();

            return (avg, trend);
        }

        private static DateTime StartOfWeek(DateTime dt)
        {
            var diff = ((int)dt.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            return dt.Date.AddDays(-diff);
        }

        /// <summary>
        /// Average idle-bed time in hours between one patient's release and the next patient's
        /// assignment on the same bed. Only counts gaps where the prior release falls in the
        /// requested window — spans passed in should NOT be pre-filtered by date beyond that,
        /// since correct pairing needs each bed's full assignment history.
        /// </summary>
        public static decimal ComputeBedTurnaroundHours(IReadOnlyList<BedSpan> spans, DateTime fromDateUtc, DateTime toDateUtc)
        {
            var gaps = new List<decimal>();
            foreach (var group in spans.GroupBy(s => s.BedId))
            {
                var ordered = group.OrderBy(s => s.AssignedAt).ToList();
                for (var i = 0; i < ordered.Count - 1; i++)
                {
                    var prior = ordered[i];
                    var next = ordered[i + 1];
                    if (prior.ReleasedAt.HasValue
                        && prior.ReleasedAt.Value >= fromDateUtc && prior.ReleasedAt.Value <= toDateUtc
                        && next.AssignedAt > prior.ReleasedAt.Value)
                    {
                        gaps.Add((decimal)(next.AssignedAt - prior.ReleasedAt.Value).TotalHours);
                    }
                }
            }
            return gaps.Count == 0 ? 0m : Math.Round(gaps.Average(), 1);
        }

        /// <summary>
        /// Average hours between an admission's ->DISCHARGE_INITIATED transition and its later
        /// ->&lt;terminal&gt; transition. Only admissions with both milestones are included — the
        /// dedicated discharge endpoint can go straight to DISCHARGED without an explicit
        /// DISCHARGE_INITIATED step, same limitation GetIrdaiDischargeClocksHandler already has.
        /// </summary>
        public static decimal ComputeDischargeTatHours(IReadOnlyList<(DateTime InitiatedAt, DateTime TerminalAt)> pairs)
        {
            if (pairs.Count == 0) return 0m;
            return Math.Round(pairs.Average(p => (decimal)(p.TerminalAt - p.InitiatedAt).TotalHours), 1);
        }

        /// <summary>
        /// Index population = clean DISCHARGED admissions in the window. A readmission = the same
        /// patient has a later admission within windowDays (fixed 30, the CMS standard) after that
        /// discharge. laterAdmissionsByPatient should include ALL of a patient's admission dates,
        /// not date-range-limited, since a readmission can fall after the requested range's end.
        /// </summary>
        public static (int ReadmittedCount, int TotalIndexCount, decimal RatePercent) ComputeReadmissionRate(
            IReadOnlyList<(string PatientId, DateTime DischargedAt)> indexDischarges,
            IReadOnlyDictionary<string, List<DateTime>> laterAdmissionsByPatient,
            int windowDays = 30)
        {
            if (indexDischarges.Count == 0) return (0, 0, 0m);

            var readmitted = 0;
            foreach (var idx in indexDischarges)
            {
                if (laterAdmissionsByPatient.TryGetValue(idx.PatientId, out var admits)
                    && admits.Any(a => a > idx.DischargedAt && a <= idx.DischargedAt.AddDays(windowDays)))
                {
                    readmitted++;
                }
            }

            var rate = Math.Round((decimal)readmitted / indexDischarges.Count * 100m, 1);
            return (readmitted, indexDischarges.Count, rate);
        }
    }
}
