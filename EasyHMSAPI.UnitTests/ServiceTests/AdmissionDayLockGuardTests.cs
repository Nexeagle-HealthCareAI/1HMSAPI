using System;
using System.Collections.Generic;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Entities;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.ServiceTests
{
    [TestFixture]
    public class AdmissionDayLockGuardTests
    {
        private static readonly DateTime NowUtc = new(2026, 8, 23, 10, 0, 0, DateTimeKind.Utc);

        [Test]
        public void FindBlockingClosedDay_NoClosedDays_ReturnsNull()
        {
            var blocking = AdmissionDayLockGuard.FindBlockingClosedDay(new List<AdmissionDayBill>(), NowUtc.AddDays(-3));

            Assert.That(blocking, Is.Null);
        }

        [Test]
        public void FindBlockingClosedDay_DateOnOrAfterLatestClosedWindow_ReturnsNull()
        {
            var day1 = new AdmissionDayBill { DayNumber = 1, FromUtc = NowUtc.AddDays(-2), ToUtc = NowUtc.AddDays(-1), InterimBillNo = "IB-1" };

            var blocking = AdmissionDayLockGuard.FindBlockingClosedDay(new[] { day1 }, day1.ToUtc);

            Assert.That(blocking, Is.Null);
        }

        [Test]
        public void FindBlockingClosedDay_DateBeforeLatestClosedWindow_ReturnsEarliestClosedDay()
        {
            var day1 = new AdmissionDayBill { DayNumber = 1, FromUtc = NowUtc.AddDays(-3), ToUtc = NowUtc.AddDays(-2), InterimBillNo = "IB-1" };
            var day2 = new AdmissionDayBill { DayNumber = 2, FromUtc = NowUtc.AddDays(-2), ToUtc = NowUtc.AddDays(-1), InterimBillNo = "IB-2" };

            // A date before Day 2's window (but inside Day 1's) must still cite Day 1 -- that's the
            // day whose Reopen action actually unlocks charge-posting for this date.
            var blocking = AdmissionDayLockGuard.FindBlockingClosedDay(new[] { day1, day2 }, day1.FromUtc.AddHours(1));

            Assert.That(blocking, Is.Not.Null);
            Assert.That(blocking!.DayNumber, Is.EqualTo(1));
            Assert.That(blocking.InterimBillNo, Is.EqualTo("IB-1"));
        }
    }
}
