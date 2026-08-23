using System;
using System.Collections.Generic;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Entities;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.ServiceTests
{
    [TestFixture]
    public class BillingBackdateGuardTests
    {
        private static readonly DateTime NowUtc = new(2026, 8, 23, 10, 0, 0, DateTimeKind.Utc);

        [Test]
        public void ValidateDate_NullDate_PassesThroughAsNow_NotBackdated()
        {
            var result = BillingBackdateGuard.ValidateDate(null, null, NowUtc);

            Assert.That(result.Success, Is.True);
            Assert.That(result.EffectiveDate, Is.EqualTo(NowUtc));
            Assert.That(result.IsBackdated, Is.False);
        }

        [Test]
        public void ValidateDate_FutureDate_Rejected()
        {
            var result = BillingBackdateGuard.ValidateDate(NowUtc.AddDays(1), "reason", NowUtc);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Does.Contain("future"));
        }

        [Test]
        public void ValidateDate_PastDate_WithoutReason_Rejected()
        {
            var result = BillingBackdateGuard.ValidateDate(NowUtc.AddDays(-3), null, NowUtc);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Does.Contain("reason"));
        }

        [Test]
        public void ValidateDate_PastDate_WithBlankReason_Rejected()
        {
            var result = BillingBackdateGuard.ValidateDate(NowUtc.AddDays(-3), "   ", NowUtc);

            Assert.That(result.Success, Is.False);
        }

        [Test]
        public void ValidateDate_PastDate_WithReason_Accepted_MarkedBackdated()
        {
            var past = NowUtc.AddDays(-3);
            var result = BillingBackdateGuard.ValidateDate(past, "Missed billing this OPD visit", NowUtc);

            Assert.That(result.Success, Is.True);
            Assert.That(result.EffectiveDate, Is.EqualTo(past));
            Assert.That(result.IsBackdated, Is.True);
        }

        [Test]
        public void ValidateDate_SameCalendarDay_EarlierTimeOfDay_NotConsideredBackdated()
        {
            // The floor is calendar-date, not exact timestamp -- entering "this morning's" charge
            // this afternoon shouldn't demand a reason.
            var earlierToday = NowUtc.AddHours(-4);
            var result = BillingBackdateGuard.ValidateDate(earlierToday, null, NowUtc);

            Assert.That(result.Success, Is.True);
            Assert.That(result.IsBackdated, Is.False);
        }

        [Test]
        public void FindBlockingClosedDay_NoClosedDays_ReturnsNull()
        {
            var blocking = BillingBackdateGuard.FindBlockingClosedDay(new List<AdmissionDayBill>(), NowUtc.AddDays(-3));

            Assert.That(blocking, Is.Null);
        }

        [Test]
        public void FindBlockingClosedDay_DateOnOrAfterLatestClosedWindow_ReturnsNull()
        {
            var day1 = new AdmissionDayBill { DayNumber = 1, FromUtc = NowUtc.AddDays(-2), ToUtc = NowUtc.AddDays(-1), InterimBillNo = "IB-1" };

            var blocking = BillingBackdateGuard.FindBlockingClosedDay(new[] { day1 }, day1.ToUtc);

            Assert.That(blocking, Is.Null);
        }

        [Test]
        public void FindBlockingClosedDay_DateBeforeLatestClosedWindow_ReturnsEarliestClosedDay()
        {
            var day1 = new AdmissionDayBill { DayNumber = 1, FromUtc = NowUtc.AddDays(-3), ToUtc = NowUtc.AddDays(-2), InterimBillNo = "IB-1" };
            var day2 = new AdmissionDayBill { DayNumber = 2, FromUtc = NowUtc.AddDays(-2), ToUtc = NowUtc.AddDays(-1), InterimBillNo = "IB-2" };

            // A date before Day 2's window (but inside Day 1's) must still cite Day 1 -- that's the
            // day whose Reopen action actually unlocks charge-posting for this date.
            var blocking = BillingBackdateGuard.FindBlockingClosedDay(new[] { day1, day2 }, day1.FromUtc.AddHours(1));

            Assert.That(blocking, Is.Not.Null);
            Assert.That(blocking!.DayNumber, Is.EqualTo(1));
            Assert.That(blocking.InterimBillNo, Is.EqualTo("IB-1"));
        }
    }
}
