namespace EasyHMSAPI.Application.Services
{
    public record DailyPatientCount(DateTime Date, int TotalAppointments, int UniquePatients);
    public record SpecialtyTrend(string SpecialtyName, int Last30Days, int Prior30Days, decimal ChangePercent, bool IsSurging);

    public record PatientVolumeTrendSummary(
        decimal Avg7DayAppointments,
        decimal Avg30DayAppointments,
        decimal Avg7DayUniquePatients,
        decimal Avg30DayUniquePatients,
        decimal MonthOverMonthAppointmentChangePercent,
        decimal MonthOverMonthUniquePatientChangePercent,
        decimal PredictedNext7DayAppointments,
        decimal PredictedNext7DayUniquePatients,
        List<SpecialtyTrend> SpecialtyTrends,
        List<DailyPatientCount> ProjectedNext7Days
    );

    /// <summary>
    /// Pure, deterministic trend math over a hospital's daily appointment/patient history -- the
    /// actual "prediction" behind the AI Patient Volume Forecast panel. No AI involved here; Groq
    /// only narrates these already-computed numbers (see GroqPatientVolumeInsightService), it
    /// never invents them. Mirrors BillingTrendCalculator's shape and static-pure-function
    /// convention, with two deliberate differences: a 7-day (not 30-day) forecast horizon --
    /// staffing decisions are short-term, unlike a monthly revenue cycle -- and day-of-week
    /// seasonality in the projection, since hospitals have real weekly visit patterns that a flat
    /// carry-forward would miss.
    /// </summary>
    public static class PatientVolumeTrendCalculator
    {
        private const int SurgeThresholdPercent = 20; // a specialty up 20%+ month-over-month is flagged as likely needing more staffing
        private const int OverloadThresholdPercent = 25; // a doctor whose predicted week exceeds their own typical week by 25%+ is flagged as overloaded

        /// <summary>Compares a doctor's own predicted next-7-day load against their own typical
        /// week (their 30-day daily average x 7) -- "overloaded" is relative to that doctor's usual
        /// pace, not a fixed number, so it's fair across doctors with very different caseloads.</summary>
        public static bool IsOverloaded(decimal predictedNext7Day, decimal avg30DayDaily)
        {
            var typicalWeek = avg30DayDaily * 7m;
            if (typicalWeek <= 0m) return false;
            return predictedNext7Day > typicalWeek * (1 + OverloadThresholdPercent / 100m);
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
        /// Naive trend-continuation forecast, same explainable philosophy as BillingTrendCalculator:
        /// each of the next 7 days starts from that weekday's historical average (falling back to
        /// the flat 7-day average when a weekday has no history yet, e.g. a new hospital), then is
        /// adjusted by the ratio of the 7-day average to the 30-day average (the recent trend
        /// direction), clamped to +/-50% so a short noisy spike/dip can't run away.
        /// </summary>
        public static PatientVolumeTrendSummary Compute(
            List<DailyPatientCount> last90Days,
            Dictionary<string, List<(DateTime Date, int Count)>> appointmentsBySpecialty)
        {
            var avg7Appt = MovingAverage(last90Days, 7);
            var avg30Appt = MovingAverage(last90Days, 30);
            var avg7Unique = MovingAverage(last90Days, 7, useUniquePatients: true);
            var avg30Unique = MovingAverage(last90Days, 30, useUniquePatients: true);

            var trendMultiplierAppt = avg30Appt > 0 ? Clamp(avg7Appt / avg30Appt, 0.5m, 1.5m) : 1m;
            var trendMultiplierUnique = avg30Unique > 0 ? Clamp(avg7Unique / avg30Unique, 0.5m, 1.5m) : 1m;

            var projected = new List<DailyPatientCount>();
            var startDate = DateTime.UtcNow.Date.AddDays(1);
            for (var i = 0; i < 7; i++)
            {
                var date = startDate.AddDays(i);

                var baselineAppt = WeekdayAverage(last90Days, date.DayOfWeek);
                if (baselineAppt == 0m) baselineAppt = avg7Appt;
                var baselineUnique = WeekdayAverage(last90Days, date.DayOfWeek, useUniquePatients: true);
                if (baselineUnique == 0m) baselineUnique = avg7Unique;

                projected.Add(new DailyPatientCount(
                    date,
                    (int)Math.Round(baselineAppt * trendMultiplierAppt),
                    (int)Math.Round(baselineUnique * trendMultiplierUnique)
                ));
            }

            return new PatientVolumeTrendSummary(
                Avg7DayAppointments: Math.Round(avg7Appt, 1),
                Avg30DayAppointments: Math.Round(avg30Appt, 1),
                Avg7DayUniquePatients: Math.Round(avg7Unique, 1),
                Avg30DayUniquePatients: Math.Round(avg30Unique, 1),
                MonthOverMonthAppointmentChangePercent: MonthOverMonthChangePercent(last90Days),
                MonthOverMonthUniquePatientChangePercent: MonthOverMonthChangePercent(last90Days, useUniquePatients: true),
                PredictedNext7DayAppointments: projected.Sum(p => p.TotalAppointments),
                PredictedNext7DayUniquePatients: projected.Sum(p => p.UniquePatients),
                SpecialtyTrends: ComputeSpecialtyTrends(appointmentsBySpecialty),
                ProjectedNext7Days: projected
            );
        }

        private static decimal Clamp(decimal value, decimal min, decimal max) => value < min ? min : value > max ? max : value;
    }
}
