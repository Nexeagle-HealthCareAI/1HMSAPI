using System;
using System.Collections.Generic;
using System.Linq;
using EasyHMSAPI.Application.Services;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.ServiceTests
{
    [TestFixture]
    public class KpiAnomalyDetectorTests
    {
        private static List<DailyOperationalStats> BuildDays(int totalDays, Func<int, (int Total, int NoShow, int Cancelled)> valueForDayIndexFromToday)
        {
            var today = DateTime.UtcNow.Date;
            var days = new List<DailyOperationalStats>();
            for (var i = totalDays - 1; i >= 0; i--)
            {
                var (total, noShow, cancelled) = valueForDayIndexFromToday(i);
                days.Add(new DailyOperationalStats(today.AddDays(-i), total, noShow, cancelled));
            }
            return days;
        }

        [Test]
        public void DetectAnomalies_StableAppointmentVolume_NoAnomalyFlagged()
        {
            // 90 flat days: 10 appointments/day, no-show/cancel patterns constant too.
            var days = BuildDays(90, i => (10, 2, 1));

            var flags = KpiAnomalyDetector.DetectAnomalies(days);

            Assert.That(flags, Is.Empty, "A perfectly stable series has zero baseline variance and must never be flagged.");
        }

        [Test]
        public void DetectAnomalies_RecentWeekSpikesFarAboveBaseline_IsFlaggedUp()
        {
            var today = DateTime.UtcNow.Date;
            var days = new List<DailyOperationalStats>();
            // 11 baseline weeks, each week's daily count constant within that week but alternating
            // 9/11 day-count week-to-week -- real week-to-week variance (weekly totals 63 vs 77),
            // unlike a pattern that repeats identically every 7 days, which would leave the
            // baseline with zero variance (nothing meaningful to compare an anomaly against).
            for (var daysAgo = 89; daysAgo >= 7; daysAgo--)
            {
                var weekIndex = daysAgo / 7; // constant across each 7-day bucket DetectAnomalies will form
                var dailyValue = weekIndex % 2 == 0 ? 9 : 11;
                days.Add(new DailyOperationalStats(today.AddDays(-daysAgo), dailyValue, 1, 1));
            }
            // Most recent 7 days spike hard, far outside that 63-77 weekly-total range.
            for (var daysAgo = 6; daysAgo >= 0; daysAgo--)
                days.Add(new DailyOperationalStats(today.AddDays(-daysAgo), 40, 1, 1));

            var flags = KpiAnomalyDetector.DetectAnomalies(days);

            var volumeFlag = flags.SingleOrDefault(f => f.MetricName == "Appointment volume");
            Assert.That(volumeFlag, Is.Not.Null, "A week at ~4x the baseline weekly total must be flagged.");
            Assert.That(volumeFlag!.Direction, Is.EqualTo("UP"));
        }

        [Test]
        public void DetectAnomalies_InsufficientHistory_ReturnsEmpty()
        {
            var days = BuildDays(20, i => (10, 2, 1)); // fewer than the 4 weeks required

            var flags = KpiAnomalyDetector.DetectAnomalies(days);

            Assert.That(flags, Is.Empty);
        }

        [Test]
        public void DetectAnomalies_NoShowRateSpikes_IsFlaggedOnNoShowRateNotJustVolume()
        {
            var today = DateTime.UtcNow.Date;
            var days = new List<DailyOperationalStats>();
            // Baseline: 10 appointments/day always, but no-shows alternate 1/day vs 2/day by week
            // (10% vs 20% rate) -- real week-to-week variance, so the baseline has a non-zero
            // stddev to compare the recent week's rate against.
            for (var daysAgo = 89; daysAgo >= 7; daysAgo--)
            {
                var weekIndex = daysAgo / 7;
                var noShow = weekIndex % 2 == 0 ? 1 : 2;
                days.Add(new DailyOperationalStats(today.AddDays(-daysAgo), 10, noShow, 0));
            }
            // Recent week: same volume, but no-shows jump to 6/day (60% rate) -- far outside the 10-20% baseline range.
            for (var daysAgo = 6; daysAgo >= 0; daysAgo--) days.Add(new DailyOperationalStats(today.AddDays(-daysAgo), 10, 6, 0));

            var flags = KpiAnomalyDetector.DetectAnomalies(days);

            var noShowFlag = flags.SingleOrDefault(f => f.MetricName == "No-show rate");
            Assert.That(noShowFlag, Is.Not.Null, "A no-show rate jumping from 10% to 60% must be flagged even though total volume didn't change.");
            Assert.That(noShowFlag!.Direction, Is.EqualTo("UP"));
        }
    }
}
