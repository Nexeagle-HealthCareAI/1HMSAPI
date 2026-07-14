using System;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class DeviceDaysCalculatorTests
    {
        private static readonly DateTime RangeStart = new DateTime(2026, 1, 1);
        private static readonly DateTime RangeEnd = new DateTime(2026, 1, 11); // 10-day window

        [Test]
        public void ComputeOverlapDays_SpanFullyInsideRange_ReturnsSpanLength()
        {
            var days = DeviceDaysCalculator.ComputeOverlapDays(
                insertedAt: new DateTime(2026, 1, 3), removedAtOrNow: new DateTime(2026, 1, 5),
                rangeStart: RangeStart, rangeEnd: RangeEnd);
            Assert.That(days, Is.EqualTo(2m));
        }

        [Test]
        public void ComputeOverlapDays_OpenEndedSpan_ClampsToRangeEnd()
        {
            var days = DeviceDaysCalculator.ComputeOverlapDays(
                insertedAt: new DateTime(2026, 1, 8), removedAtOrNow: new DateTime(2026, 2, 1), // still "in place" (now)
                rangeStart: RangeStart, rangeEnd: RangeEnd);
            Assert.That(days, Is.EqualTo(3m));
        }

        [Test]
        public void ComputeOverlapDays_SpanFullyBeforeRange_ReturnsZero()
        {
            var days = DeviceDaysCalculator.ComputeOverlapDays(
                insertedAt: new DateTime(2025, 12, 1), removedAtOrNow: new DateTime(2025, 12, 20),
                rangeStart: RangeStart, rangeEnd: RangeEnd);
            Assert.That(days, Is.EqualTo(0m));
        }

        [Test]
        public void ComputeOverlapDays_SpanFullyAfterRange_ReturnsZero()
        {
            var days = DeviceDaysCalculator.ComputeOverlapDays(
                insertedAt: new DateTime(2026, 2, 1), removedAtOrNow: new DateTime(2026, 2, 10),
                rangeStart: RangeStart, rangeEnd: RangeEnd);
            Assert.That(days, Is.EqualTo(0m));
        }

        [Test]
        public void ComputeOverlapDays_PartialOverlapAtStart_ClampsToRangeStart()
        {
            var days = DeviceDaysCalculator.ComputeOverlapDays(
                insertedAt: new DateTime(2025, 12, 29), removedAtOrNow: new DateTime(2026, 1, 4),
                rangeStart: RangeStart, rangeEnd: RangeEnd);
            Assert.That(days, Is.EqualTo(3m));
        }

        [Test]
        public void ComputeOverlapDays_PartialOverlapAtEnd_ClampsToRangeEnd()
        {
            var days = DeviceDaysCalculator.ComputeOverlapDays(
                insertedAt: new DateTime(2026, 1, 9), removedAtOrNow: new DateTime(2026, 1, 20),
                rangeStart: RangeStart, rangeEnd: RangeEnd);
            Assert.That(days, Is.EqualTo(2m));
        }
    }
}
