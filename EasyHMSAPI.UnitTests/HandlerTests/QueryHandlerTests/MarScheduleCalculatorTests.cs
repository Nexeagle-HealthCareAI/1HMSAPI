using System;
using System.Linq;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class MarScheduleCalculatorTests
    {
        // 2026-07-01 00:00 IST == 2026-06-30 18:30 UTC.
        private static readonly DateTime OrderedAtUtc = new DateTime(2026, 6, 30, 18, 30, 0, DateTimeKind.Utc);
        private static readonly DateTime WindowStartUtc = OrderedAtUtc;
        private static readonly DateTime WindowEndUtc = OrderedAtUtc.AddDays(10);

        [Test]
        public void ComputeSlots_Bd_ReturnsTwoSlotsPerDayAtWardClockTimes()
        {
            var slots = MarScheduleCalculator.ComputeSlots("BD", OrderedAtUtc, 1, WindowStartUtc, WindowEndUtc);

            Assert.That(slots, Has.Count.EqualTo(2));
            // 08:00 IST == 02:30 UTC; 20:00 IST == 14:30 UTC, both on the order's IST calendar day.
            Assert.That(slots[0], Is.EqualTo(new DateTime(2026, 7, 1, 2, 30, 0, DateTimeKind.Utc)));
            Assert.That(slots[1], Is.EqualTo(new DateTime(2026, 7, 1, 14, 30, 0, DateTimeKind.Utc)));
        }

        [Test]
        public void ComputeSlots_Tds_ReturnsThreeSlotsPerDay()
        {
            var slots = MarScheduleCalculator.ComputeSlots("TDS", OrderedAtUtc, 1, WindowStartUtc, WindowEndUtc);
            Assert.That(slots, Has.Count.EqualTo(3));
        }

        [Test]
        public void ComputeSlots_Qid_ReturnsFourSlotsPerDay()
        {
            var slots = MarScheduleCalculator.ComputeSlots("QID", OrderedAtUtc, 1, WindowStartUtc, WindowEndUtc);
            Assert.That(slots, Has.Count.EqualTo(4));
        }

        [Test]
        public void ComputeSlots_Od_SpansMultipleDaysAcrossDurationDays()
        {
            var slots = MarScheduleCalculator.ComputeSlots("OD", OrderedAtUtc, 3, WindowStartUtc, WindowEndUtc);
            // OrderedAtUtc is exactly IST midnight, so DurationDays=3 bounds the window to the
            // next 3 IST midnights — one OD slot per full day covered: 07-01, 07-02, 07-03.
            Assert.That(slots, Has.Count.EqualTo(3));
        }

        [Test]
        public void ComputeSlots_Q4h_RollsForwardFromOrderTimeNotWardClock()
        {
            var slots = MarScheduleCalculator.ComputeSlots("Q4H", OrderedAtUtc, 1, WindowStartUtc, WindowEndUtc);

            Assert.That(slots.First(), Is.EqualTo(OrderedAtUtc));
            Assert.That(slots[1], Is.EqualTo(OrderedAtUtc.AddHours(4)));
            Assert.That(slots[2], Is.EqualTo(OrderedAtUtc.AddHours(8)));
        }

        [Test]
        public void ComputeSlots_Stat_ReturnsSingleSlotAtOrderTime()
        {
            var slots = MarScheduleCalculator.ComputeSlots("STAT", OrderedAtUtc, null, WindowStartUtc, WindowEndUtc);
            Assert.That(slots, Has.Count.EqualTo(1));
            Assert.That(slots[0], Is.EqualTo(OrderedAtUtc));
        }

        [Test]
        public void ComputeSlots_Sos_ReturnsNoSlots()
        {
            var slots = MarScheduleCalculator.ComputeSlots("SOS", OrderedAtUtc, 5, WindowStartUtc, WindowEndUtc);
            Assert.That(slots, Is.Empty);
        }

        [Test]
        public void ComputeSlots_UnrecognizedFreeTextFrequency_ReturnsNoSlots()
        {
            // Pre-Phase-4 orders may have arbitrary free text like "once daily" — no computed slots.
            var slots = MarScheduleCalculator.ComputeSlots("once daily", OrderedAtUtc, 5, WindowStartUtc, WindowEndUtc);
            Assert.That(slots, Is.Empty);
        }

        [Test]
        public void ComputeSlots_NullFrequency_ReturnsNoSlots()
        {
            var slots = MarScheduleCalculator.ComputeSlots(null, OrderedAtUtc, 5, WindowStartUtc, WindowEndUtc);
            Assert.That(slots, Is.Empty);
        }

        [Test]
        public void ComputeSlots_DurationDaysNull_IsBoundedOnlyByWindow()
        {
            var slots = MarScheduleCalculator.ComputeSlots("OD", OrderedAtUtc, null, WindowStartUtc, OrderedAtUtc.AddDays(3));
            // No DurationDays to bound the line, so the caller-supplied window (3 days from an
            // IST-midnight order time) is the only bound: one OD slot per full day covered.
            Assert.That(slots, Has.Count.EqualTo(3));
        }

        [Test]
        public void ComputeSlots_WindowNarrowerThanDuration_TruncatesToWindow()
        {
            // Cuts off partway through day 2, after day 2's 08:00 BD slot but before its 20:00 one.
            var narrowEnd = OrderedAtUtc.AddDays(1).AddHours(9);
            var slots = MarScheduleCalculator.ComputeSlots("BD", OrderedAtUtc, 10, WindowStartUtc, narrowEnd);
            // Day 1's two slots + day 2's 08:00 slot fall within the narrowed window.
            Assert.That(slots, Has.Count.EqualTo(3));
        }

        [Test]
        public void DeriveSlotStatus_MoreThan15MinBeforeDue_IsPending()
        {
            var scheduled = new DateTime(2026, 7, 1, 8, 0, 0, DateTimeKind.Utc);
            var now = scheduled.AddMinutes(-20);
            Assert.That(MarScheduleCalculator.DeriveSlotStatus(scheduled, now), Is.EqualTo("PENDING"));
        }

        [Test]
        public void DeriveSlotStatus_WithinDueWindow_IsDue()
        {
            var scheduled = new DateTime(2026, 7, 1, 8, 0, 0, DateTimeKind.Utc);
            Assert.That(MarScheduleCalculator.DeriveSlotStatus(scheduled, scheduled.AddMinutes(-10)), Is.EqualTo("DUE"));
            Assert.That(MarScheduleCalculator.DeriveSlotStatus(scheduled, scheduled), Is.EqualTo("DUE"));
            Assert.That(MarScheduleCalculator.DeriveSlotStatus(scheduled, scheduled.AddMinutes(45)), Is.EqualTo("DUE"));
        }

        [Test]
        public void DeriveSlotStatus_PastMatchToleranceWithinGrace_IsOverdue()
        {
            var scheduled = new DateTime(2026, 7, 1, 8, 0, 0, DateTimeKind.Utc);
            Assert.That(MarScheduleCalculator.DeriveSlotStatus(scheduled, scheduled.AddMinutes(46)), Is.EqualTo("OVERDUE"));
            Assert.That(MarScheduleCalculator.DeriveSlotStatus(scheduled, scheduled.AddHours(3)), Is.EqualTo("OVERDUE"));
        }

        [Test]
        public void DeriveSlotStatus_PastMissedThreshold_IsMissed()
        {
            var scheduled = new DateTime(2026, 7, 1, 8, 0, 0, DateTimeKind.Utc);
            Assert.That(MarScheduleCalculator.DeriveSlotStatus(scheduled, scheduled.AddHours(3).AddMinutes(1)), Is.EqualTo("MISSED"));
        }
    }
}
