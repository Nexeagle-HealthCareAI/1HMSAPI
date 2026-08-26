namespace EasyHMSAPI.Application.Services
{
    public record DailyPatientCount(DateTime Date, int TotalAppointments, int UniquePatients);
    public record SpecialtyTrend(string SpecialtyName, int Last30Days, int Prior30Days, decimal ChangePercent, bool IsSurging);
    public record MonthlySeasonalFactor(int Month, string MonthName, decimal Index, bool IsNotable);

    public record PatientVolumeTrendSummary(
        decimal Avg7DayAppointments,
        decimal Avg30DayAppointments,
        decimal Avg7DayUniquePatients,
        decimal Avg30DayUniquePatients,
        decimal MonthOverMonthAppointmentChangePercent,
        decimal MonthOverMonthUniquePatientChangePercent,
        decimal PredictedNext30DayAppointments,
        decimal PredictedNext30DayUniquePatients,
        List<SpecialtyTrend> SpecialtyTrends,
        List<MonthlySeasonalFactor> MonthlySeasonalFactors,
        List<DailyPatientCount> ProjectedNext30Days
    );

    /// <summary>
    /// Pure, deterministic trend math over a hospital's daily appointment/patient history -- the
    /// actual "prediction" behind the AI Patient Volume Forecast panel. No AI involved here; Groq
    /// only narrates these already-computed numbers (see GroqPatientVolumeInsightService), it
    /// never invents them. Mirrors BillingTrendCalculator's shape and static-pure-function
    /// convention (same 30-day forecast horizon).
    ///
    /// Three multiplicative factors make up a projected day, each answering a different question
    /// over a different amount of history (classic trend x seasonal decomposition):
    ///   - Day-of-week baseline, from the last 365 days (or all history if shorter) -- "what does a
    ///     typical week look like RIGHT NOW." Old data is a poor guide here: e.g. if Saturday clinic
    ///     hours started a year ago, appointments from 3 years back would show zero Saturday volume
    ///     and wrongly drag the average down.
    ///   - Month-of-year seasonal index, from ALL available history -- "does this calendar month
    ///     structurally behave differently every year" (e.g. a quieter December). This is exactly
    ///     where more history helps: a month needs to recur across years before it's a real pattern
    ///     and not noise from one unusual year.
    ///   - Short-term trend multiplier (7-day avg / 30-day avg, clamped 0.5x-1.5x) -- already
    ///     recency-scoped, catches recent momentum the other two factors can't.
    /// Each factor is independently clamped so they can't compound into something absurd, and each
    /// falls back to neutral (no adjustment) when there isn't enough history to trust it -- the same
    /// "insufficient data stays quiet" convention used throughout this class.
    /// </summary>
    public static class PatientVolumeTrendCalculator
    {
        private const int ForecastHorizonDays = 30;
        private const int SurgeThresholdPercent = 20; // a specialty up 20%+ month-over-month is flagged as likely needing more staffing
        private const int OverloadThresholdPercent = 25; // a doctor whose predicted month exceeds their own typical month by 25%+ is flagged as overloaded
        private const int WeekdayBaselineWindowDays = 365; // "typical week" is judged from the last year, not all-time
        private const int MinDaysForMonthlySeasonality = 20; // total days (across all years) needed before trusting a calendar month's index
        private const decimal MonthlySeasonalIndexMin = 0.7m;
        private const decimal MonthlySeasonalIndexMax = 1.3m;
        private const decimal NotableSeasonalIndexLowerBound = 0.9m;
        private const decimal NotableSeasonalIndexUpperBound = 1.1m;

        /// <summary>Compares a doctor's own predicted next-30-day load against their own typical
        /// month (their 30-day daily average x 30) -- "overloaded" is relative to that doctor's usual
        /// pace, not a fixed number, so it's fair across doctors with very different caseloads.</summary>
        public static bool IsOverloaded(decimal predictedNext30Day, decimal avg30DayDaily)
        {
            var typicalMonth = avg30DayDaily * ForecastHorizonDays;
            if (typicalMonth <= 0m) return false;
            return predictedNext30Day > typicalMonth * (1 + OverloadThresholdPercent / 100m);
        }

        public static decimal MovingAverage(IReadOnlyList<DailyPatientCount> days, int windowDays, bool useUniquePatients = false)
        {
            if (days.Count == 0 || windowDays <= 0) return 0m;
            var window = days.OrderByDescending(d => d.Date).Take(windowDays).ToList();
            if (window.Count == 0) return 0m;
            return useUniquePatients ? (decimal)window.Average(d => d.UniquePatients) : (decimal)window.Average(d => d.TotalAppointments);
        }

        /// <summary>Historical average for a specific weekday (e.g. every Monday in the window) -- the
        /// seasonal baseline the projection starts from before applying the recent trend multiplier.</summary>
        public static decimal WeekdayAverage(IReadOnlyList<DailyPatientCount> days, DayOfWeek dayOfWeek, bool useUniquePatients = false)
        {
            var matching = days.Where(d => d.Date.DayOfWeek == dayOfWeek).ToList();
            if (matching.Count == 0) return 0m;
            return useUniquePatients ? (decimal)matching.Average(d => d.UniquePatients) : (decimal)matching.Average(d => d.TotalAppointments);
        }

        /// <summary>Percent change between the sum of the most recent 30 days and the 30 before that.</summary>
        public static decimal MonthOverMonthChangePercent(IReadOnlyList<DailyPatientCount> days, bool useUniquePatients = false)
        {
            var ordered = days.OrderByDescending(d => d.Date).ToList();
            var last30 = ordered.Take(30).Sum(d => useUniquePatients ? d.UniquePatients : d.TotalAppointments);
            var prior30 = ordered.Skip(30).Take(30).Sum(d => useUniquePatients ? d.UniquePatients : d.TotalAppointments);
            if (prior30 == 0) return 0m;
            return Math.Round((last30 - prior30) / (decimal)prior30 * 100m, 1);
        }

        /// <summary>
        /// Per-specialty month-over-month trend, flagging any specialty up 20%+ as "surging" --
        /// the concrete, explainable signal behind "this specialty may need more staffing."
        /// </summary>
        public static List<SpecialtyTrend> ComputeSpecialtyTrends(IReadOnlyDictionary<string, List<(DateTime Date, int Count)>> bySpecialty)
        {
            var result = new List<SpecialtyTrend>();
            var cutoffLast = DateTime.UtcNow.Date;
            foreach (var (specialty, points) in bySpecialty)
            {
                var ordered = points.OrderByDescending(p => p.Date).ToList();
                var last30 = ordered.Where(p => p.Date > cutoffLast.AddDays(-30)).Sum(p => p.Count);
                var prior30 = ordered.Where(p => p.Date <= cutoffLast.AddDays(-30) && p.Date > cutoffLast.AddDays(-60)).Sum(p => p.Count);
                decimal changePercent = prior30 == 0 ? 0m : Math.Round((last30 - prior30) / (decimal)prior30 * 100m, 1);
                result.Add(new SpecialtyTrend(specialty, last30, prior30, changePercent, changePercent >= SurgeThresholdPercent));
            }
            return result.OrderByDescending(s => s.ChangePercent).ToList();
        }

        /// <summary>
        /// A calendar month's index = (that month's average daily volume, across every year present)
        /// / (the overall average daily volume across all history). 1.0 = no seasonal effect; below 1
        /// = historically quieter; above 1 = historically busier. Requires at least
        /// MinDaysForMonthlySeasonality total observed days for that month before trusting it --
        /// with only a few months of history most calendar months simply won't appear here, which is
        /// the correct, safe outcome (neutral wherever looked up), not an error.
        /// </summary>
        public static Dictionary<int, decimal> ComputeMonthlySeasonalIndex(IReadOnlyList<DailyPatientCount> allDays, bool useUniquePatients = false)
        {
            var result = new Dictionary<int, decimal>();
            if (allDays.Count == 0) return result;

            var overallAvg = useUniquePatients ? (decimal)allDays.Average(d => d.UniquePatients) : (decimal)allDays.Average(d => d.TotalAppointments);
            if (overallAvg <= 0m) return result;

            foreach (var group in allDays.GroupBy(d => d.Date.Month))
            {
                var daysInGroup = group.ToList();
                if (daysInGroup.Count < MinDaysForMonthlySeasonality) continue;

                var monthAvg = useUniquePatients ? (decimal)daysInGroup.Average(d => d.UniquePatients) : (decimal)daysInGroup.Average(d => d.TotalAppointments);
                var rawIndex = monthAvg / overallAvg;
                result[group.Key] = Math.Round(Clamp(rawIndex, MonthlySeasonalIndexMin, MonthlySeasonalIndexMax), 2);
            }

            return result;
        }

        private static List<MonthlySeasonalFactor> BuildMonthlySeasonalFactors(Dictionary<int, decimal> indexByMonth)
        {
            return indexByMonth
                .OrderBy(kv => kv.Key)
                .Select(kv => new MonthlySeasonalFactor(
                    kv.Key,
                    new DateTime(2000, kv.Key, 1).ToString("MMMM"),
                    kv.Value,
                    kv.Value < NotableSeasonalIndexLowerBound || kv.Value > NotableSeasonalIndexUpperBound
                ))
                .ToList();
        }

        /// <summary>
        /// Naive trend-continuation forecast, same explainable philosophy as BillingTrendCalculator:
        /// each of the next 30 days starts from that weekday's seasonal baseline (last 365 days),
        /// adjusted by that day's month-of-year seasonal index (all available history) and the
        /// recent 7-vs-30-day trend multiplier -- see the class doc comment for why each factor uses
        /// a different amount of history. <paramref name="allDays"/> should be the hospital's (or
        /// doctor's) full available appointment history, zero-filled with no gaps, not just a recent
        /// slice -- callers no longer pre-trim to 90 days.
        /// </summary>
        public static PatientVolumeTrendSummary Compute(
            List<DailyPatientCount> allDays,
            Dictionary<string, List<(DateTime Date, int Count)>> appointmentsBySpecialty)
        {
            var avg7Appt = MovingAverage(allDays, 7);
            var avg30Appt = MovingAverage(allDays, 30);
            var avg7Unique = MovingAverage(allDays, 7, useUniquePatients: true);
            var avg30Unique = MovingAverage(allDays, 30, useUniquePatients: true);

            var trendMultiplierAppt = avg30Appt > 0 ? Clamp(avg7Appt / avg30Appt, 0.5m, 1.5m) : 1m;
            var trendMultiplierUnique = avg30Unique > 0 ? Clamp(avg7Unique / avg30Unique, 0.5m, 1.5m) : 1m;

            var weekdayBaselineCutoff = DateTime.UtcNow.Date.AddDays(-WeekdayBaselineWindowDays);
            var recentForWeekday = allDays.Where(d => d.Date >= weekdayBaselineCutoff).ToList();
            if (recentForWeekday.Count == 0) recentForWeekday = allDays; // hospital's data starts in the future relative to "today" in a test, or is otherwise all older -- fall back to whatever exists

            var monthIndexAppt = ComputeMonthlySeasonalIndex(allDays);
            var monthIndexUnique = ComputeMonthlySeasonalIndex(allDays, useUniquePatients: true);

            var projected = new List<DailyPatientCount>();
            var startDate = DateTime.UtcNow.Date.AddDays(1);
            for (var i = 0; i < ForecastHorizonDays; i++)
            {
                var date = startDate.AddDays(i);

                var baselineAppt = WeekdayAverage(recentForWeekday, date.DayOfWeek);
                if (baselineAppt == 0m) baselineAppt = avg7Appt;
                var baselineUnique = WeekdayAverage(recentForWeekday, date.DayOfWeek, useUniquePatients: true);
                if (baselineUnique == 0m) baselineUnique = avg7Unique;

                var seasonalApptIndex = monthIndexAppt.TryGetValue(date.Month, out var mi) ? mi : 1m;
                var seasonalUniqueIndex = monthIndexUnique.TryGetValue(date.Month, out var mu) ? mu : 1m;

                projected.Add(new DailyPatientCount(
                    date,
                    (int)Math.Round(baselineAppt * trendMultiplierAppt * seasonalApptIndex),
                    (int)Math.Round(baselineUnique * trendMultiplierUnique * seasonalUniqueIndex)
                ));
            }

            return new PatientVolumeTrendSummary(
                Avg7DayAppointments: Math.Round(avg7Appt, 1),
                Avg30DayAppointments: Math.Round(avg30Appt, 1),
                Avg7DayUniquePatients: Math.Round(avg7Unique, 1),
                Avg30DayUniquePatients: Math.Round(avg30Unique, 1),
                MonthOverMonthAppointmentChangePercent: MonthOverMonthChangePercent(allDays),
                MonthOverMonthUniquePatientChangePercent: MonthOverMonthChangePercent(allDays, useUniquePatients: true),
                PredictedNext30DayAppointments: projected.Sum(p => p.TotalAppointments),
                PredictedNext30DayUniquePatients: projected.Sum(p => p.UniquePatients),
                SpecialtyTrends: ComputeSpecialtyTrends(appointmentsBySpecialty),
                MonthlySeasonalFactors: BuildMonthlySeasonalFactors(monthIndexAppt),
                ProjectedNext30Days: projected
            );
        }

        private static decimal Clamp(decimal value, decimal min, decimal max) => value < min ? min : value > max ? max : value;
    }
}
