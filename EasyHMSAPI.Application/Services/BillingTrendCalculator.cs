namespace EasyHMSAPI.Application.Services
{
    public record DailyAmount(DateTime Date, decimal Revenue, decimal Expense);
    public record CategoryTrend(string CategoryCode, decimal Last30Days, decimal Prior30Days, decimal ChangePercent, bool IsLeak);

    public record TrendSummary(
        decimal Avg7DayRevenue,
        decimal Avg30DayRevenue,
        decimal Avg7DayExpense,
        decimal Avg30DayExpense,
        decimal MonthOverMonthRevenueChangePercent,
        decimal MonthOverMonthExpenseChangePercent,
        decimal PredictedNext30DayRevenue,
        decimal PredictedNext30DayExpense,
        List<CategoryTrend> RevenueCategoryTrends,
        List<DailyAmount> ProjectedNext30Days
    );

    /// <summary>
    /// Pure, deterministic trend math over a hospital's daily revenue/expense history -- the actual
    /// "prediction" behind the AI Predictive Analysis tab. No AI involved here; Groq only narrates
    /// these already-computed numbers (see GroqBillingInsightService), it never invents them. Kept
    /// as static pure functions (no DbContext) so every branch is directly unit-testable, matching
    /// this codebase's existing GstTaxComputer/AdmissionDayLockGuard convention.
    /// </summary>
    public static class BillingTrendCalculator
    {
        private const int LeakThresholdPercent = -10; // a category down 10%+ month-over-month is flagged

        public static decimal MovingAverage(IReadOnlyList<DailyAmount> days, int windowDays, bool useExpense = false)
        {
            if (days.Count == 0 || windowDays <= 0) return 0m;
            var window = days.OrderByDescending(d => d.Date).Take(windowDays).ToList();
            if (window.Count == 0) return 0m;
            return useExpense ? window.Average(d => d.Expense) : window.Average(d => d.Revenue);
        }

        /// <summary>Percent change between the sum of the most recent 30 days and the 30 before that.</summary>
        public static decimal MonthOverMonthChangePercent(IReadOnlyList<DailyAmount> days, bool useExpense = false)
        {
            var ordered = days.OrderByDescending(d => d.Date).ToList();
            var last30 = ordered.Take(30).Sum(d => useExpense ? d.Expense : d.Revenue);
            var prior30 = ordered.Skip(30).Take(30).Sum(d => useExpense ? d.Expense : d.Revenue);
            if (prior30 == 0m) return 0m;
            return Math.Round((last30 - prior30) / prior30 * 100m, 1);
        }

        /// <summary>
        /// Per-category month-over-month trend, flagging any category down 10%+ as a "leak" --
        /// the concrete, explainable signal behind "highlight where money is leaking."
        /// </summary>
        public static List<CategoryTrend> CategoryTrends(IReadOnlyDictionary<string, List<(DateTime Date, decimal Amount)>> byCategory)
        {
            var result = new List<CategoryTrend>();
            var cutoffLast = DateTime.UtcNow.Date;
            foreach (var (category, points) in byCategory)
            {
                var ordered = points.OrderByDescending(p => p.Date).ToList();
                var last30 = ordered.Where(p => p.Date > cutoffLast.AddDays(-30)).Sum(p => p.Amount);
                var prior30 = ordered.Where(p => p.Date <= cutoffLast.AddDays(-30) && p.Date > cutoffLast.AddDays(-60)).Sum(p => p.Amount);
                decimal changePercent = prior30 == 0m ? 0m : Math.Round((last30 - prior30) / prior30 * 100m, 1);
                result.Add(new CategoryTrend(category, last30, prior30, changePercent, changePercent <= LeakThresholdPercent));
            }
            return result.OrderBy(c => c.ChangePercent).ToList();
        }

        /// <summary>
        /// Simple, explainable projection: the next 30 days each get the most recent 7-day daily
        /// average, adjusted by the ratio of the 7-day average to the 30-day average (the recent
        /// trend direction), clamped to +/-50% so a short noisy spike/dip can't run away. This is a
        /// naive trend-continuation forecast, not a statistical model -- appropriate for "where is
        /// this heading if nothing changes," not a guarantee.
        /// </summary>
        public static TrendSummary Compute(
            List<DailyAmount> last90Days,
            Dictionary<string, List<(DateTime Date, decimal Amount)>> revenueByCategory)
        {
            var avg7Rev = MovingAverage(last90Days, 7);
            var avg30Rev = MovingAverage(last90Days, 30);
            var avg7Exp = MovingAverage(last90Days, 7, useExpense: true);
            var avg30Exp = MovingAverage(last90Days, 30, useExpense: true);

            var trendMultiplierRev = avg30Rev > 0 ? Clamp(avg7Rev / avg30Rev, 0.5m, 1.5m) : 1m;
            var trendMultiplierExp = avg30Exp > 0 ? Clamp(avg7Exp / avg30Exp, 0.5m, 1.5m) : 1m;

            var projectedDailyRev = avg7Rev * trendMultiplierRev;
            var projectedDailyExp = avg7Exp * trendMultiplierExp;

            var projected = new List<DailyAmount>();
            var startDate = DateTime.UtcNow.Date.AddDays(1);
            for (var i = 0; i < 30; i++)
            {
                projected.Add(new DailyAmount(startDate.AddDays(i), Math.Round(projectedDailyRev, 2), Math.Round(projectedDailyExp, 2)));
            }

            return new TrendSummary(
                Avg7DayRevenue: Math.Round(avg7Rev, 2),
                Avg30DayRevenue: Math.Round(avg30Rev, 2),
                Avg7DayExpense: Math.Round(avg7Exp, 2),
                Avg30DayExpense: Math.Round(avg30Exp, 2),
                MonthOverMonthRevenueChangePercent: MonthOverMonthChangePercent(last90Days),
                MonthOverMonthExpenseChangePercent: MonthOverMonthChangePercent(last90Days, useExpense: true),
                PredictedNext30DayRevenue: Math.Round(projectedDailyRev * 30, 2),
                PredictedNext30DayExpense: Math.Round(projectedDailyExp * 30, 2),
                RevenueCategoryTrends: CategoryTrends(revenueByCategory),
                ProjectedNext30Days: projected
            );
        }

        private static decimal Clamp(decimal value, decimal min, decimal max) => value < min ? min : value > max ? max : value;
    }
}
