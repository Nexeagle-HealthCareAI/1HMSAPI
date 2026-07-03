using System;
using System.Collections.Generic;
using System.Linq;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class IpdKpiCalculatorTests
    {
        private static readonly Guid BedA = Guid.NewGuid();
        private static readonly Guid BedB = Guid.NewGuid();

        // ── BOR ──────────────────────────────────────────────────────────────
        [Test]
        public void ComputeBorSeries_NoAssignments_ReturnsZeroForEveryDay()
        {
            var series = IpdKpiCalculator.ComputeBorSeries(new List<IpdKpiCalculator.BedSpan>(), totalActiveBeds: 10,
                fromDateUtc: new DateTime(2026, 7, 1), toDateUtc: new DateTime(2026, 7, 3));

            Assert.That(series, Has.Count.EqualTo(3));
            Assert.That(series.All(p => p.BorPercent == 0m), Is.True);
        }

        [Test]
        public void ComputeBorSeries_ZeroActiveBeds_ReturnsZeroNotDivideByZero()
        {
            var spans = new List<IpdKpiCalculator.BedSpan> { new(BedA, new DateTime(2026, 7, 1), null) };
            var series = IpdKpiCalculator.ComputeBorSeries(spans, totalActiveBeds: 0,
                fromDateUtc: new DateTime(2026, 7, 1), toDateUtc: new DateTime(2026, 7, 1));

            Assert.That(series[0].BorPercent, Is.EqualTo(0m));
        }

        [Test]
        public void ComputeBorSeries_OneOfTwoBedsOccupiedWholeDay_ReturnsFiftyPercent()
        {
            var spans = new List<IpdKpiCalculator.BedSpan> { new(BedA, new DateTime(2026, 6, 1), null) };
            var series = IpdKpiCalculator.ComputeBorSeries(spans, totalActiveBeds: 2,
                fromDateUtc: new DateTime(2026, 7, 1), toDateUtc: new DateTime(2026, 7, 1));

            Assert.That(series[0].BorPercent, Is.EqualTo(50m));
        }

        [Test]
        public void ComputeBorSeries_BedReleasedBeforeDayStart_NotCountedOccupied()
        {
            var spans = new List<IpdKpiCalculator.BedSpan> { new(BedA, new DateTime(2026, 6, 25), new DateTime(2026, 6, 30)) };
            var series = IpdKpiCalculator.ComputeBorSeries(spans, totalActiveBeds: 1,
                fromDateUtc: new DateTime(2026, 7, 1), toDateUtc: new DateTime(2026, 7, 1));

            Assert.That(series[0].BorPercent, Is.EqualTo(0m));
        }

        // ── ALOS ─────────────────────────────────────────────────────────────
        [Test]
        public void ComputeAlos_NoAdmissions_ReturnsZeroAndEmptyTrend()
        {
            var (avg, trend) = IpdKpiCalculator.ComputeAlos(new List<(DateTime, DateTime)>());
            Assert.That(avg, Is.EqualTo(0m));
            Assert.That(trend, Is.Empty);
        }

        [Test]
        public void ComputeAlos_TwoAdmissions_AveragesCorrectly()
        {
            var admissions = new List<(DateTime AdmittedAt, DateTime DischargedAt)>
            {
                (new DateTime(2026, 7, 1), new DateTime(2026, 7, 3)),   // 2 days
                (new DateTime(2026, 7, 1), new DateTime(2026, 7, 5)),   // 4 days
            };
            var (avg, _) = IpdKpiCalculator.ComputeAlos(admissions);
            Assert.That(avg, Is.EqualTo(3m));
        }

        // ── Bed turnaround ───────────────────────────────────────────────────
        [Test]
        public void ComputeBedTurnaroundHours_NoGapsInWindow_ReturnsZero()
        {
            var spans = new List<IpdKpiCalculator.BedSpan> { new(BedA, new DateTime(2026, 7, 1), null) };
            var hours = IpdKpiCalculator.ComputeBedTurnaroundHours(spans, new DateTime(2026, 7, 1), new DateTime(2026, 7, 10));
            Assert.That(hours, Is.EqualTo(0m));
        }

        [Test]
        public void ComputeBedTurnaroundHours_OneGap_ComputesHoursBetweenReleaseAndNextAssignment()
        {
            var spans = new List<IpdKpiCalculator.BedSpan>
            {
                new(BedA, new DateTime(2026, 7, 1, 8, 0, 0), new DateTime(2026, 7, 2, 10, 0, 0)),
                new(BedA, new DateTime(2026, 7, 2, 16, 0, 0), null),   // 6h gap after release
            };
            var hours = IpdKpiCalculator.ComputeBedTurnaroundHours(spans, new DateTime(2026, 7, 1), new DateTime(2026, 7, 10));
            Assert.That(hours, Is.EqualTo(6m));
        }

        [Test]
        public void ComputeBedTurnaroundHours_ReleaseOutsideWindow_Excluded()
        {
            var spans = new List<IpdKpiCalculator.BedSpan>
            {
                new(BedA, new DateTime(2026, 6, 1), new DateTime(2026, 6, 2, 10, 0, 0)),
                new(BedA, new DateTime(2026, 6, 2, 16, 0, 0), null),
            };
            var hours = IpdKpiCalculator.ComputeBedTurnaroundHours(spans, new DateTime(2026, 7, 1), new DateTime(2026, 7, 10));
            Assert.That(hours, Is.EqualTo(0m));
        }

        [Test]
        public void ComputeBedTurnaroundHours_MultipleBeds_AveragesAcrossAll()
        {
            var spans = new List<IpdKpiCalculator.BedSpan>
            {
                new(BedA, new DateTime(2026, 7, 1, 8, 0, 0), new DateTime(2026, 7, 2, 8, 0, 0)),
                new(BedA, new DateTime(2026, 7, 2, 10, 0, 0), null),   // 2h gap
                new(BedB, new DateTime(2026, 7, 1, 8, 0, 0), new DateTime(2026, 7, 2, 8, 0, 0)),
                new(BedB, new DateTime(2026, 7, 2, 12, 0, 0), null),   // 4h gap
            };
            var hours = IpdKpiCalculator.ComputeBedTurnaroundHours(spans, new DateTime(2026, 7, 1), new DateTime(2026, 7, 10));
            Assert.That(hours, Is.EqualTo(3m));
        }

        // ── Discharge TAT ────────────────────────────────────────────────────
        [Test]
        public void ComputeDischargeTatHours_NoPairs_ReturnsZero()
        {
            Assert.That(IpdKpiCalculator.ComputeDischargeTatHours(new List<(DateTime, DateTime)>()), Is.EqualTo(0m));
        }

        [Test]
        public void ComputeDischargeTatHours_OnePair_ComputesHours()
        {
            var pairs = new List<(DateTime InitiatedAt, DateTime TerminalAt)>
            {
                (new DateTime(2026, 7, 1, 9, 0, 0), new DateTime(2026, 7, 1, 15, 0, 0)),
            };
            Assert.That(IpdKpiCalculator.ComputeDischargeTatHours(pairs), Is.EqualTo(6m));
        }

        // ── Readmission rate ─────────────────────────────────────────────────
        [Test]
        public void ComputeReadmissionRate_NoIndexDischarges_ReturnsZero()
        {
            var (readmitted, total, rate) = IpdKpiCalculator.ComputeReadmissionRate(
                new List<(string, DateTime)>(), new Dictionary<string, List<DateTime>>());
            Assert.That(readmitted, Is.EqualTo(0));
            Assert.That(total, Is.EqualTo(0));
            Assert.That(rate, Is.EqualTo(0m));
        }

        [Test]
        public void ComputeReadmissionRate_ReadmittedWithinWindow_CountsAsReadmission()
        {
            var dischargedAt = new DateTime(2026, 7, 1);
            var index = new List<(string PatientId, DateTime DischargedAt)> { ("P1", dischargedAt) };
            var later = new Dictionary<string, List<DateTime>> { ["P1"] = new() { dischargedAt.AddDays(10) } };

            var (readmitted, total, rate) = IpdKpiCalculator.ComputeReadmissionRate(index, later);
            Assert.That(readmitted, Is.EqualTo(1));
            Assert.That(total, Is.EqualTo(1));
            Assert.That(rate, Is.EqualTo(100m));
        }

        [Test]
        public void ComputeReadmissionRate_ReadmissionExactlyAtThirtyDayBoundary_CountsAsReadmission()
        {
            var dischargedAt = new DateTime(2026, 7, 1);
            var index = new List<(string PatientId, DateTime DischargedAt)> { ("P1", dischargedAt) };
            var later = new Dictionary<string, List<DateTime>> { ["P1"] = new() { dischargedAt.AddDays(30) } };

            var (readmitted, _, _) = IpdKpiCalculator.ComputeReadmissionRate(index, later, windowDays: 30);
            Assert.That(readmitted, Is.EqualTo(1));
        }

        [Test]
        public void ComputeReadmissionRate_ReadmissionJustPastThirtyDayBoundary_NotCounted()
        {
            var dischargedAt = new DateTime(2026, 7, 1);
            var index = new List<(string PatientId, DateTime DischargedAt)> { ("P1", dischargedAt) };
            var later = new Dictionary<string, List<DateTime>> { ["P1"] = new() { dischargedAt.AddDays(31) } };

            var (readmitted, _, rate) = IpdKpiCalculator.ComputeReadmissionRate(index, later, windowDays: 30);
            Assert.That(readmitted, Is.EqualTo(0));
            Assert.That(rate, Is.EqualTo(0m));
        }

        [Test]
        public void ComputeReadmissionRate_SameAdmissionDateAsDischarge_NotCountedAsLater()
        {
            // A patient's own index admission's AdmittedAt should never itself count as "later."
            var dischargedAt = new DateTime(2026, 7, 5);
            var index = new List<(string PatientId, DateTime DischargedAt)> { ("P1", dischargedAt) };
            var later = new Dictionary<string, List<DateTime>> { ["P1"] = new() { dischargedAt } };

            var (readmitted, _, _) = IpdKpiCalculator.ComputeReadmissionRate(index, later);
            Assert.That(readmitted, Is.EqualTo(0));
        }
    }
}
