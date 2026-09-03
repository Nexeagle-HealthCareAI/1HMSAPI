using System;
using System.Collections.Generic;
using System.Linq;
using EasyHMSAPI.Application.Services;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.ServiceTests
{
    [TestFixture]
    public class BillingTrendCalculatorTests
    {
        private static List<DailyAmount> ConstantSeries(int days, decimal revenue, decimal expense)
        {
            var today = DateTime.UtcNow.Date;
            return Enumerable.Range(0, days)
                .Select(i => new DailyAmount(today.AddDays(-days + 1 + i), revenue, expense))
                .ToList();
        }

        [Test]
        public void MovingAverage_ConstantSeries_ReturnsThatConstant()
        {
            var days = ConstantSeries(90, 1000m, 200m);

            Assert.That(BillingTrendCalculator.MovingAverage(days, 7), Is.EqualTo(1000m));
            Assert.That(BillingTrendCalculator.MovingAverage(days, 30), Is.EqualTo(1000m));
            Assert.That(BillingTrendCalculator.MovingAverage(days, 7, useExpense: true), Is.EqualTo(200m));
        }

        [Test]
        public void MovingAverage_EmptySeries_ReturnsZero()
        {
            Assert.That(BillingTrendCalculator.MovingAverage(new List<DailyAmount>(), 7), Is.EqualTo(0m));
        }

        [Test]
        public void MonthOverMonthChangePercent_FlatSeries_IsZero()
        {
            var days = ConstantSeries(90, 1000m, 200m);
            Assert.That(BillingTrendCalculator.MonthOverMonthChangePercent(days), Is.EqualTo(0m));
        }

        [Test]
        public void MonthOverMonthChangePercent_DoubledRecentRevenue_Is100Percent()
        {
            var today = DateTime.UtcNow.Date;
            var days = new List<DailyAmount>();
            // Days 60-31 ago: 1000/day. Days 30-1 ago: 2000/day (double).
            for (var i = 60; i >= 1; i--)
            {
                var revenue = i <= 30 ? 2000m : 1000m;
                days.Add(new DailyAmount(today.AddDays(-i), revenue, 0m));
            }

            var change = BillingTrendCalculator.MonthOverMonthChangePercent(days);
            Assert.That(change, Is.EqualTo(100m));
        }

        [Test]
        public void CategoryTrends_DecliningCategory_IsFlaggedAsLeak()
        {
            var today = DateTime.UtcNow.Date;
            // CategoryTrends filters "last 30" as date > today-30, and "prior 30" as
            // today-60 < date <= today-30. Deliberately skip i=60 and i=30 themselves so each
            // window lands exactly 29 points wide with no boundary ambiguity.
            var pharmacyPoints = new List<(DateTime Date, decimal Amount)>();
            for (var i = 59; i >= 31; i--) pharmacyPoints.Add((today.AddDays(-i), 1000m));
            for (var i = 29; i >= 1; i--) pharmacyPoints.Add((today.AddDays(-i), 500m)); // -50%

            var otPoints = new List<(DateTime Date, decimal Amount)>();
            for (var i = 59; i >= 31; i--) otPoints.Add((today.AddDays(-i), 1000m));
            for (var i = 29; i >= 1; i--) otPoints.Add((today.AddDays(-i), 1500m)); // +50%, growing

            var byCategory = new Dictionary<string, List<(DateTime Date, decimal Amount)>>
            {
                ["PHARMACY"] = pharmacyPoints,
                ["OT"] = otPoints,
            };

            var trends = BillingTrendCalculator.CategoryTrends(byCategory);

            var pharmacy = trends.Single(t => t.CategoryCode == "PHARMACY");
            Assert.That(pharmacy.ChangePercent, Is.EqualTo(-50m));
            Assert.That(pharmacy.IsLeak, Is.True, "A 50% decline must be flagged as a leak.");

            var ot = trends.Single(t => t.CategoryCode == "OT");
            Assert.That(ot.ChangePercent, Is.EqualTo(50m));
            Assert.That(ot.IsLeak, Is.False, "A growing category must not be flagged as a leak.");
        }

        [Test]
        public void Compute_FlatHistory_ProjectsTheSameDailyAverage()
        {
            var days = ConstantSeries(90, 1000m, 200m);
            var summary = BillingTrendCalculator.Compute(days, new Dictionary<string, List<(DateTime, decimal)>>());

            Assert.That(summary.Avg7DayRevenue, Is.EqualTo(1000m));
            Assert.That(summary.Avg30DayRevenue, Is.EqualTo(1000m));
            // Flat history -> trend multiplier is 1 -> predicted totals are exactly Nx the daily average.
            Assert.That(summary.PredictedTomorrowRevenue, Is.EqualTo(1000m));
            Assert.That(summary.PredictedTomorrowExpense, Is.EqualTo(200m));
            Assert.That(summary.PredictedNext7DayRevenue, Is.EqualTo(7000m));
            Assert.That(summary.PredictedNext7DayExpense, Is.EqualTo(1400m));
            Assert.That(summary.PredictedNext30DayRevenue, Is.EqualTo(30000m));
            Assert.That(summary.PredictedNext30DayExpense, Is.EqualTo(6000m));
            Assert.That(summary.ProjectedNext30Days, Has.Count.EqualTo(30));
        }

        [Test]
        public void Compute_ProjectedDates_StartTomorrowAndAreConsecutive()
        {
            var days = ConstantSeries(90, 500m, 100m);
            var summary = BillingTrendCalculator.Compute(days, new Dictionary<string, List<(DateTime, decimal)>>());

            var tomorrow = DateTime.UtcNow.Date.AddDays(1);
            Assert.That(summary.ProjectedNext30Days[0].Date, Is.EqualTo(tomorrow));
            Assert.That(summary.ProjectedNext30Days[29].Date, Is.EqualTo(tomorrow.AddDays(29)));
        }

        [Test]
        public void Compute_ExtremeSpike_ProjectionIsClampedNotRunaway()
        {
            var today = DateTime.UtcNow.Date;
            var days = new List<DailyAmount>();
            for (var i = 89; i >= 8; i--) days.Add(new DailyAmount(today.AddDays(-i), 100m, 0m));
            // Last 7 days spike to 100x -- without clamping this would massively overproject.
            for (var i = 7; i >= 0; i--) days.Add(new DailyAmount(today.AddDays(-i), 10000m, 0m));

            var summary = BillingTrendCalculator.Compute(days, new Dictionary<string, List<(DateTime, decimal)>>());

            // trendMultiplier is clamped to 1.5 max, so projected daily <= avg7Rev * 1.5.
            var maxPlausibleDaily = summary.Avg7DayRevenue * 1.5m;
            Assert.That(summary.PredictedNext30DayRevenue, Is.LessThanOrEqualTo(maxPlausibleDaily * 30m + 1m));
        }
    }
}
