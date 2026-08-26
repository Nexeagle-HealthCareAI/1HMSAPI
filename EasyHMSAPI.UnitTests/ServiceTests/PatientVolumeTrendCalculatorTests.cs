using System;
using System.Collections.Generic;
using System.Linq;
using EasyHMSAPI.Application.Services;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.ServiceTests
{
    [TestFixture]
    public class PatientVolumeTrendCalculatorTests
    {
        private static List<DailyPatientCount> ConstantSeries(int days, int appointments, int uniquePatients)
        {
            var today = DateTime.UtcNow.Date;
            return Enumerable.Range(0, days)
                .Select(i => new DailyPatientCount(today.AddDays(-days + 1 + i), appointments, uniquePatients))
                .ToList();
        }

        [Test]
        public void MovingAverage_ConstantSeries_ReturnsThatConstant()
        {
            var days = ConstantSeries(90, 50, 40);

            Assert.That(PatientVolumeTrendCalculator.MovingAverage(days, 7), Is.EqualTo(50m));
            Assert.That(PatientVolumeTrendCalculator.MovingAverage(days, 30), Is.EqualTo(50m));
            Assert.That(PatientVolumeTrendCalculator.MovingAverage(days, 7, useUniquePatients: true), Is.EqualTo(40m));
        }

        [Test]
        public void MovingAverage_EmptySeries_ReturnsZero()
        {
            Assert.That(PatientVolumeTrendCalculator.MovingAverage(new List<DailyPatientCount>(), 7), Is.EqualTo(0m));
        }

        [Test]
        public void MonthOverMonthChangePercent_FlatSeries_IsZero()
        {
            var days = ConstantSeries(90, 50, 40);
            Assert.That(PatientVolumeTrendCalculator.MonthOverMonthChangePercent(days), Is.EqualTo(0m));
        }

        [Test]
        public void MonthOverMonthChangePercent_DoubledRecentVolume_Is100Percent()
        {
            var today = DateTime.UtcNow.Date;
            var days = new List<DailyPatientCount>();
            // Days 60-31 ago: 50/day. Days 30-1 ago: 100/day (double).
            for (var i = 60; i >= 1; i--)
            {
                var count = i <= 30 ? 100 : 50;
                days.Add(new DailyPatientCount(today.AddDays(-i), count, count));
            }

            var change = PatientVolumeTrendCalculator.MonthOverMonthChangePercent(days);
            Assert.That(change, Is.EqualTo(100m));
        }

        [Test]
        public void WeekdayAverage_MondayHeavySeries_ReturnsMondaySpecificAverage()
        {
            var today = DateTime.UtcNow.Date;
            var days = new List<DailyPatientCount>();
            for (var i = 89; i >= 0; i--)
            {
                var date = today.AddDays(-i);
                var count = date.DayOfWeek == DayOfWeek.Monday ? 100 : 40;
                days.Add(new DailyPatientCount(date, count, count));
            }

            var mondayNonMonday = new[] { DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday }
                .First(d => d != DayOfWeek.Monday);

            Assert.That(PatientVolumeTrendCalculator.WeekdayAverage(days, DayOfWeek.Monday), Is.EqualTo(100m));
            Assert.That(PatientVolumeTrendCalculator.WeekdayAverage(days, mondayNonMonday), Is.EqualTo(40m));
        }

        [Test]
        public void WeekdayAverage_NoHistoryForThatWeekday_ReturnsZero()
        {
            Assert.That(PatientVolumeTrendCalculator.WeekdayAverage(new List<DailyPatientCount>(), DayOfWeek.Sunday), Is.EqualTo(0m));
        }

        [Test]
        public void ComputeSpecialtyTrends_SurgingSpecialty_IsFlagged()
        {
            var today = DateTime.UtcNow.Date;
            // Same "29 points wide, boundaries skipped" convention as BillingTrendCalculatorTests'
            // CategoryTrends test, so last30/prior30 sums land exactly with no off-by-one ambiguity.
            var cardiologyPoints = new List<(DateTime Date, int Count)>();
            for (var i = 59; i >= 31; i--) cardiologyPoints.Add((today.AddDays(-i), 10));
            for (var i = 29; i >= 1; i--) cardiologyPoints.Add((today.AddDays(-i), 13)); // +30%

            var orthoPoints = new List<(DateTime Date, int Count)>();
            for (var i = 59; i >= 31; i--) orthoPoints.Add((today.AddDays(-i), 10));
            for (var i = 29; i >= 1; i--) orthoPoints.Add((today.AddDays(-i), 10)); // flat

            var bySpecialty = new Dictionary<string, List<(DateTime Date, int Count)>>
            {
                ["Cardiology"] = cardiologyPoints,
                ["Orthopedics"] = orthoPoints,
            };

            var trends = PatientVolumeTrendCalculator.ComputeSpecialtyTrends(bySpecialty);

            var cardiology = trends.Single(t => t.SpecialtyName == "Cardiology");
            Assert.That(cardiology.ChangePercent, Is.EqualTo(30m));
            Assert.That(cardiology.IsSurging, Is.True, "A 30% increase must be flagged as surging.");

            var ortho = trends.Single(t => t.SpecialtyName == "Orthopedics");
            Assert.That(ortho.ChangePercent, Is.EqualTo(0m));
            Assert.That(ortho.IsSurging, Is.False, "A flat specialty must not be flagged as surging.");
        }

        [Test]
        public void Compute_FlatHistory_ProjectsTheSameDailyAverage()
        {
            var days = ConstantSeries(90, 50, 40);
            var summary = PatientVolumeTrendCalculator.Compute(days, new Dictionary<string, List<(DateTime, int)>>());

            Assert.That(summary.Avg7DayAppointments, Is.EqualTo(50m));
            Assert.That(summary.Avg30DayAppointments, Is.EqualTo(50m));
            // Flat history -> trend multiplier is 1 -> predicted 30-day total is exactly 30x the daily average.
            Assert.That(summary.PredictedNext30DayAppointments, Is.EqualTo(1500m));
            Assert.That(summary.PredictedNext30DayUniquePatients, Is.EqualTo(1200m));
            Assert.That(summary.ProjectedNext30Days, Has.Count.EqualTo(30));
        }

        [Test]
        public void Compute_ProjectedDates_StartTomorrowAndAreConsecutive()
        {
            var days = ConstantSeries(90, 20, 15);
            var summary = PatientVolumeTrendCalculator.Compute(days, new Dictionary<string, List<(DateTime, int)>>());

            var tomorrow = DateTime.UtcNow.Date.AddDays(1);
            Assert.That(summary.ProjectedNext30Days[0].Date, Is.EqualTo(tomorrow));
            Assert.That(summary.ProjectedNext30Days[29].Date, Is.EqualTo(tomorrow.AddDays(29)));
        }

        [Test]
        public void Compute_ExtremeSpike_ProjectionIsClampedNotRunaway()
        {
            var today = DateTime.UtcNow.Date;
            var days = new List<DailyPatientCount>();
            for (var i = 89; i >= 8; i--) days.Add(new DailyPatientCount(today.AddDays(-i), 10, 8));
            // Last 7 days spike to 50x -- without clamping this would massively overproject.
            for (var i = 7; i >= 0; i--) days.Add(new DailyPatientCount(today.AddDays(-i), 500, 400));

            var summary = PatientVolumeTrendCalculator.Compute(days, new Dictionary<string, List<(DateTime, int)>>());

            // trendMultiplier is clamped to 1.5 max, so projected daily <= avg7Appt * 1.5.
            var maxPlausibleDaily = summary.Avg7DayAppointments * 1.5m;
            Assert.That(summary.PredictedNext30DayAppointments, Is.LessThanOrEqualTo(maxPlausibleDaily * 30m + 30m));
        }

        [Test]
        public void Compute_MondayHeavySeries_ProjectsHigherMondayThanOtherDays()
        {
            var today = DateTime.UtcNow.Date;
            var days = new List<DailyPatientCount>();
            for (var i = 89; i >= 0; i--)
            {
                var date = today.AddDays(-i);
                var count = date.DayOfWeek == DayOfWeek.Monday ? 100 : 40;
                days.Add(new DailyPatientCount(date, count, count));
            }

            var summary = PatientVolumeTrendCalculator.Compute(days, new Dictionary<string, List<(DateTime, int)>>());

            var mondayProjection = summary.ProjectedNext30Days.First(d => d.Date.DayOfWeek == DayOfWeek.Monday);
            var nonMondayProjection = summary.ProjectedNext30Days.First(d => d.Date.DayOfWeek != DayOfWeek.Monday);

            Assert.That(mondayProjection.TotalAppointments, Is.GreaterThan(nonMondayProjection.TotalAppointments * 2),
                "Monday's seasonal baseline (100) must dominate a non-Monday day's (40) projection.");
        }

        [Test]
        public void IsOverloaded_PredictedMonthWellAboveTypical_ReturnsTrue()
        {
            // Typical month = 10/day * 30 = 300. Predicted 400 is +33%, above the 25% threshold.
            Assert.That(PatientVolumeTrendCalculator.IsOverloaded(400m, 10m), Is.True);
        }

        [Test]
        public void IsOverloaded_PredictedMonthWithinNormalRange_ReturnsFalse()
        {
            // Typical month = 10/day * 30 = 300. Predicted 350 is only +16.7%, below the 25% threshold.
            Assert.That(PatientVolumeTrendCalculator.IsOverloaded(350m, 10m), Is.False);
        }

        [Test]
        public void IsOverloaded_NoTypicalHistory_ReturnsFalse()
        {
            Assert.That(PatientVolumeTrendCalculator.IsOverloaded(50m, 0m), Is.False);
        }

        [Test]
        public void ComputeMonthlySeasonalIndex_EmptySeries_ReturnsEmpty()
        {
            Assert.That(PatientVolumeTrendCalculator.ComputeMonthlySeasonalIndex(new List<DailyPatientCount>()), Is.Empty);
        }

        [Test]
        public void ComputeMonthlySeasonalIndex_MonthConsistentlyDifferentAcrossYears_GetsClampedIndex()
        {
            // Fixed calendar years, deliberately independent of "today" -- June is 1.5x the overall
            // average (would clamp to 1.3), December is 0.5x (would clamp to 0.7), recurring across
            // 3 separate years so it reads as a real pattern, not one unusual year.
            var days = new List<DailyPatientCount>();
            foreach (var year in new[] { 2021, 2022, 2023 })
            {
                for (var d = 1; d <= 25; d++)
                {
                    days.Add(new DailyPatientCount(new DateTime(year, 6, d), 150, 150));
                    days.Add(new DailyPatientCount(new DateTime(year, 12, d), 50, 50));
                }
            }

            var index = PatientVolumeTrendCalculator.ComputeMonthlySeasonalIndex(days);

            Assert.That(index[6], Is.EqualTo(1.3m), "June's raw ratio (1.5x) must be clamped to the 1.3 max.");
            Assert.That(index[12], Is.EqualTo(0.7m), "December's raw ratio (0.5x) must be clamped to the 0.7 min.");
        }

        [Test]
        public void ComputeMonthlySeasonalIndex_TooFewDaysForAMonth_IsExcluded()
        {
            var days = new List<DailyPatientCount>();
            for (var d = 1; d <= 25; d++) days.Add(new DailyPatientCount(new DateTime(2023, 6, d), 100, 100));
            for (var d = 1; d <= 10; d++) days.Add(new DailyPatientCount(new DateTime(2023, 7, d), 500, 500)); // only 10 days -- below the 20-day minimum

            var index = PatientVolumeTrendCalculator.ComputeMonthlySeasonalIndex(days);

            Assert.That(index.ContainsKey(6), Is.True);
            Assert.That(index.ContainsKey(7), Is.False, "A month with fewer than 20 observed days must not get an index -- stays neutral wherever looked up.");
        }

        [Test]
        public void Compute_IncludesMonthlySeasonalFactorsInSummary()
        {
            var days = new List<DailyPatientCount>();
            foreach (var year in new[] { 2021, 2022, 2023 })
            {
                for (var d = 1; d <= 25; d++)
                {
                    days.Add(new DailyPatientCount(new DateTime(year, 6, d), 150, 150));
                    days.Add(new DailyPatientCount(new DateTime(year, 12, d), 50, 50));
                }
            }

            var summary = PatientVolumeTrendCalculator.Compute(days, new Dictionary<string, List<(DateTime, int)>>());

            var june = summary.MonthlySeasonalFactors.Single(f => f.Month == 6);
            Assert.That(june.Index, Is.EqualTo(1.3m));
            Assert.That(june.IsNotable, Is.True);
            Assert.That(june.MonthName, Is.EqualTo("June"));

            var december = summary.MonthlySeasonalFactors.Single(f => f.Month == 12);
            Assert.That(december.Index, Is.EqualTo(0.7m));
            Assert.That(december.IsNotable, Is.True);
        }

        [Test]
        public void Compute_MonthCloseToAverage_IsNotFlaggedNotable()
        {
            var days = new List<DailyPatientCount>();
            for (var d = 1; d <= 25; d++)
            {
                days.Add(new DailyPatientCount(new DateTime(2023, 3, d), 100, 100));
                days.Add(new DailyPatientCount(new DateTime(2023, 4, d), 105, 105)); // only 5% above average
            }

            var summary = PatientVolumeTrendCalculator.Compute(days, new Dictionary<string, List<(DateTime, int)>>());
            var april = summary.MonthlySeasonalFactors.Single(f => f.Month == 4);

            Assert.That(april.IsNotable, Is.False, "A month within the +/-10% notable band should not be flagged.");
        }
    }
}
