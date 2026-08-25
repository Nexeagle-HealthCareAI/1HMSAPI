namespace EasyHMSAPI.Application.Services
{
    /// <summary>
    /// Pure, deterministic anomaly check over a hospital's last-90-days operational history --
    /// splits the window into non-overlapping 7-day buckets, treats the most recent bucket as
    /// "this week" and the rest as the historical baseline, and flags a metric when this week's
    /// value is more than a z-score threshold away from that baseline's mean. No AI involved here;
    /// Groq only narrates flagged anomalies (see GroqPatientVolumeInsightService), and is
    /// explicitly told to suggest possible reasons, never assert a confirmed cause it wasn't given.
    /// </summary>
    public static class KpiAnomalyDetector
    {
        private const int MinBaselineWeeks = 3; // need at least 3 prior weeks + the recent week before judging "normal"

        public static List<AnomalyFlag> DetectAnomalies(List<DailyOperationalStats> last90Days, decimal zThreshold = 2.0m)
        {
            var ordered = last90Days.OrderByDescending(d => d.Date).ToList();
            var weeks = new List<(int TotalAppointments, int NoShow, int Cancelled)>();
            for (var w = 0; w + 7 <= ordered.Count; w += 7)
            {
                var chunk = ordered.Skip(w).Take(7).ToList();
                weeks.Add((chunk.Sum(d => d.TotalAppointments), chunk.Sum(d => d.NoShowCount), chunk.Sum(d => d.CancelledCount)));
            }

            if (weeks.Count < MinBaselineWeeks + 1) return new List<AnomalyFlag>();

            var recent = weeks[0];
            var baseline = weeks.Skip(1).ToList();

            var flags = new List<AnomalyFlag>();

            flags.AddRange(CheckMetric("Appointment volume", recent.TotalAppointments, baseline.Select(b => (decimal)b.TotalAppointments).ToList(), zThreshold));

            var recentNoShowRate = recent.TotalAppointments > 0 ? (decimal)recent.NoShow / recent.TotalAppointments * 100m : 0m;
            var baselineNoShowRates = baseline.Where(b => b.TotalAppointments > 0).Select(b => (decimal)b.NoShow / b.TotalAppointments * 100m).ToList();
            flags.AddRange(CheckMetric("No-show rate", recentNoShowRate, baselineNoShowRates, zThreshold));

            var recentCancelledRate = recent.TotalAppointments > 0 ? (decimal)recent.Cancelled / recent.TotalAppointments * 100m : 0m;
            var baselineCancelledRates = baseline.Where(b => b.TotalAppointments > 0).Select(b => (decimal)b.Cancelled / b.TotalAppointments * 100m).ToList();
            flags.AddRange(CheckMetric("Cancelled rate", recentCancelledRate, baselineCancelledRates, zThreshold));

            return flags;
        }

        private static List<AnomalyFlag> CheckMetric(string metricName, decimal recentValue, List<decimal> baselineValues, decimal zThreshold)
        {
            if (baselineValues.Count < MinBaselineWeeks) return new List<AnomalyFlag>();

            var mean = baselineValues.Average();
            var variance = baselineValues.Sum(v => (v - mean) * (v - mean)) / baselineValues.Count;
            var stdDev = (decimal)Math.Sqrt((double)variance);
            if (stdDev == 0m) return new List<AnomalyFlag>(); // no variation in the baseline to compare against

            var zScore = (recentValue - mean) / stdDev;
            if (Math.Abs(zScore) < zThreshold) return new List<AnomalyFlag>();

            return new List<AnomalyFlag>
            {
                new(metricName, Math.Round(recentValue, 1), Math.Round(mean, 1), Math.Round(stdDev, 1), Math.Round(zScore, 2), zScore > 0 ? "UP" : "DOWN")
            };
        }
    }
}
