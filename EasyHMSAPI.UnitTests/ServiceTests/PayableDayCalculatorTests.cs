using System;
using System.Collections.Generic;
using System.Linq;
using EasyHMSAPI.Application.Services;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.ServiceTests
{
    [TestFixture]
    public class PayableDayCalculatorTests
    {
        // October 2026: 31 days; Sundays = 4, 11, 18, 25.
        private static readonly PayrollPeriod Oct2026 = new(10, 2026);
        private static readonly DateOnly LongAgo = new(2020, 1, 1);

        private static IEnumerable<(DateOnly, string)> Days(string status, params int[] days) =>
            days.Select(d => (new DateOnly(2026, 10, d), status));

        private static PayrollCalendarPolicy Policy(bool offPayable = true, bool holidayPayable = true, string offDays = "SUN", params int[] holidays) =>
            new(PayrollCalendarPolicy.ParseWeeklyOffDays(offDays), offPayable, holidayPayable,
                holidays.Select(d => new DateOnly(2026, 10, d)).ToHashSet());

        [Test]
        public void Default_policy_pays_sundays_so_a_full_attendance_month_pays_every_day()
        {
            // Present on all 27 non-Sunday days -> 27 worked + 4 paid Sundays = 31.
            var worked = Enumerable.Range(1, 31).Where(d => new DateOnly(2026, 10, d).DayOfWeek != DayOfWeek.Sunday).ToArray();

            var days = PayableDayCalculator.Compute(Oct2026, LongAgo, Days("PRESENT", worked), PayrollCalendarPolicy.Default);

            Assert.That(days, Is.EqualTo(31m));
        }

        [Test]
        public void Weekly_off_not_payable_when_policy_says_so()
        {
            var worked = Enumerable.Range(1, 31).Where(d => new DateOnly(2026, 10, d).DayOfWeek != DayOfWeek.Sunday).ToArray();

            var days = PayableDayCalculator.Compute(Oct2026, LongAgo, Days("PRESENT", worked), Policy(offPayable: false));

            Assert.That(days, Is.EqualTo(27m));
        }

        [Test]
        public void Approved_leave_is_payable_and_half_day_counts_half()
        {
            var days = PayableDayCalculator.Compute(Oct2026, LongAgo,
                Days("ON_LEAVE", 1, 2, 3).Concat(Days("HALF_DAY", 5)).Concat(Days("ABSENT", 6)),
                Policy(offPayable: false, holidayPayable: false));

            Assert.That(days, Is.EqualTo(3.5m));
        }

        [Test]
        public void Declared_holiday_is_payable_by_default_and_unpaid_when_configured()
        {
            var paid = PayableDayCalculator.Compute(Oct2026, LongAgo, Array.Empty<(DateOnly, string)>(),
                Policy(offPayable: false, holidayPayable: true, offDays: "", holidays: new[] { 2, 20 }));
            var unpaid = PayableDayCalculator.Compute(Oct2026, LongAgo, Array.Empty<(DateOnly, string)>(),
                Policy(offPayable: false, holidayPayable: false, offDays: "", holidays: new[] { 2, 20 }));

            Assert.That(paid, Is.EqualTo(2m));
            Assert.That(unpaid, Is.EqualTo(0m));
        }

        [Test]
        public void A_day_that_is_both_worked_and_an_off_counts_once()
        {
            // Worked on Sunday 4th (a paid off): still 1, not 2.
            var days = PayableDayCalculator.Compute(Oct2026, LongAgo, Days("PRESENT", 4), Policy(offDays: "SUN"));

            // 1 (worked Sunday 4th) + 3 other paid Sundays (11, 18, 25).
            Assert.That(days, Is.EqualTo(4m));
        }

        [Test]
        public void Nothing_is_paid_before_the_joining_date()
        {
            var joined = new DateOnly(2026, 10, 20);

            var days = PayableDayCalculator.Compute(Oct2026, joined, Days("PRESENT", 20, 21, 22), PayrollCalendarPolicy.Default);

            // 20, 21, 22 worked + Sunday the 25th paid; Sundays 4/11/18 are before joining.
            Assert.That(days, Is.EqualTo(4m));
        }

        [Test]
        public void Weekly_off_days_round_trip_in_a_stable_order()
        {
            var parsed = PayrollCalendarPolicy.ParseWeeklyOffDays("sun, sat ,xyz");

            Assert.That(PayrollCalendarPolicy.FormatWeeklyOffDays(parsed), Is.EqualTo("SAT,SUN"));
            Assert.That(PayrollCalendarPolicy.ParseWeeklyOffDays(""), Is.Empty);
        }
    }
}
