using System;
using System.Linq;
using EasyHMSAPI.Application.Services;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.ServiceTests
{
    [TestFixture]
    public class AttendanceEngineTests
    {
        // August 2026, hospital wall-clock.
        private static DateTime T(int day, int hour, int minute, int second = 0) => new(2026, 8, day, hour, minute, second);
        private static DateOnly D(int day) => new(2026, 8, day);

        private static RosteredShift Morning(int day) => new(D(day), "SFT_M", new TimeOnly(8, 0), new TimeOnly(14, 0), 15);
        private static RosteredShift Evening(int day) => new(D(day), "SFT_E", new TimeOnly(14, 0), new TimeOnly(20, 0), 15);
        private static RosteredShift Night(int day) => new(D(day), "SFT_N", new TimeOnly(20, 0), new TimeOnly(8, 0), 30);
        private static RosteredShift General(int day) => new(D(day), "SFT_G", new TimeOnly(9, 30), new TimeOnly(17, 30), 10);

        private static readonly RosteredShift[] NoRoster = Array.Empty<RosteredShift>();

        // ─── The ordinary day ────────────────────────────────────────────────

        [Test]
        public void OnTimeFullShift_IsPresentWithHoursAndNoOvertime()
        {
            var days = AttendanceEngine.Compute(new[] { T(10, 9, 28), T(10, 17, 31) }, new[] { General(10) });

            var day = days.Single();
            Assert.That(day.WorkDate, Is.EqualTo(D(10)));
            Assert.That(day.Status, Is.EqualTo("PRESENT"));
            Assert.That(day.PunchIn, Is.EqualTo(T(10, 9, 28)));
            Assert.That(day.PunchOut, Is.EqualTo(T(10, 17, 31)));
            Assert.That(day.TotalHours, Is.EqualTo(8.05m));
            Assert.That(day.OvertimeHours, Is.EqualTo(0.05m), "hours beyond the 8h rostered shift");
            Assert.That(day.Unscheduled, Is.False);
            Assert.That(day.ShiftCode, Is.EqualTo("SFT_G"));
        }

        [Test]
        public void NoScans_GivesNoDays()
        {
            Assert.That(AttendanceEngine.Compute(Array.Empty<DateTime>(), new[] { General(10) }), Is.Empty);
        }

        [Test]
        public void ScansGivenOutOfOrder_GiveTheSameResult()
        {
            var inOrder = AttendanceEngine.Compute(new[] { T(10, 9, 30), T(10, 17, 30) }, new[] { General(10) });
            var shuffled = AttendanceEngine.Compute(new[] { T(10, 17, 30), T(10, 9, 30) }, new[] { General(10) });

            Assert.That(shuffled, Is.EqualTo(inOrder));
        }

        // ─── Late marks ──────────────────────────────────────────────────────

        [Test]
        public void ArrivingExactlyAtTheEndOfGrace_IsNotLate()
        {
            var day = AttendanceEngine.Compute(new[] { T(10, 8, 15), T(10, 14, 0) }, new[] { Morning(10) }).Single();

            Assert.That(day.Status, Is.EqualTo("PRESENT"), "grace is 15 min: 08:15 is still on time");
        }

        [Test]
        public void ArrivingOneMinutePastGrace_IsLate()
        {
            var day = AttendanceEngine.Compute(new[] { T(10, 8, 16), T(10, 14, 0) }, new[] { Morning(10) }).Single();

            Assert.That(day.Status, Is.EqualTo("LATE"));
        }

        [Test]
        public void ZeroGraceMeansStrict()
        {
            var strict = new RosteredShift(D(10), "SFT_M", new TimeOnly(8, 0), new TimeOnly(14, 0), 0);

            Assert.That(AttendanceEngine.Compute(new[] { T(10, 8, 1), T(10, 14, 0) }, new[] { strict }).Single().Status, Is.EqualTo("LATE"));
            Assert.That(AttendanceEngine.Compute(new[] { T(10, 8, 0), T(10, 14, 0) }, new[] { strict }).Single().Status, Is.EqualTo("PRESENT"));
        }

        [Test]
        public void ArrivingEarlyIsNeverLate()
        {
            var day = AttendanceEngine.Compute(new[] { T(10, 7, 20), T(10, 14, 0) }, new[] { Morning(10) }).Single();

            Assert.That(day.Status, Is.EqualTo("PRESENT"));
        }

        // ─── Night shifts (the case a calendar-day model gets wrong) ────────

        [Test]
        public void NightShiftAcrossMidnight_IsOneDayBelongingToTheDayItStarted()
        {
            var days = AttendanceEngine.Compute(new[] { T(10, 19, 55), T(11, 8, 10) }, new[] { Night(10) });

            var day = days.Single();
            Assert.That(day.WorkDate, Is.EqualTo(D(10)), "a night shift belongs to the day it begins");
            Assert.That(day.PunchIn, Is.EqualTo(T(10, 19, 55)));
            Assert.That(day.PunchOut, Is.EqualTo(T(11, 8, 10)));
            Assert.That(day.TotalHours, Is.EqualTo(12.25m));
            Assert.That(day.OvertimeHours, Is.EqualTo(0.25m), "12h rostered, worked 12h15m");
            Assert.That(day.Status, Is.EqualTo("PRESENT"));
        }

        [Test]
        public void ConsecutiveNightShifts_AreTwoSeparateDays()
        {
            var scans = new[] { T(10, 19, 58), T(11, 8, 5), T(11, 19, 59), T(12, 8, 3) };

            var days = AttendanceEngine.Compute(scans, new[] { Night(10), Night(11) });

            Assert.That(days.Select(d => d.WorkDate), Is.EqualTo(new[] { D(10), D(11) }));
            Assert.That(days.All(d => d.PunchOut.HasValue && !d.Unscheduled), Is.True);
        }

        [Test]
        public void NightShiftArrivalAfterGrace_IsLate()
        {
            var day = AttendanceEngine.Compute(new[] { T(10, 20, 31), T(11, 8, 0) }, new[] { Night(10) }).Single();

            Assert.That(day.Status, Is.EqualTo("LATE"), "night grace is 30 min: 20:31 is late");
        }

        [Test]
        public void NightShiftWithoutARoster_SplitsIntoTwoUnscheduledDays_WhichIsWhyRostersMatter()
        {
            // Documents a known limitation rather than hiding it: with nothing rostered the engine can only
            // group by calendar date, so an overnight worker gets a lone IN and a lone next-day scan.
            var days = AttendanceEngine.Compute(new[] { T(10, 19, 55), T(11, 8, 10) }, NoRoster);

            Assert.That(days, Has.Count.EqualTo(2));
            Assert.That(days.All(d => d.Unscheduled), Is.True);
        }

        // ─── Double taps, breaks, single scans ───────────────────────────────

        [Test]
        public void DoubleTapWithinTwoMinutes_IsIgnored()
        {
            var day = AttendanceEngine.Compute(new[] { T(10, 9, 30, 0), T(10, 9, 30, 40), T(10, 17, 30) }, new[] { General(10) }).Single();

            Assert.That(day.PunchIn, Is.EqualTo(T(10, 9, 30, 0)));
            Assert.That(day.PunchOut, Is.EqualTo(T(10, 17, 30)));
        }

        [Test]
        public void OnlyADoubleTap_IsStillJustOneScan_NotAnInAndOut()
        {
            var day = AttendanceEngine.Compute(new[] { T(10, 9, 30, 0), T(10, 9, 30, 40) }, new[] { General(10) }).Single();

            Assert.That(day.PunchOut, Is.Null);
            Assert.That(day.TotalHours, Is.Null);
        }

        [Test]
        public void SingleScan_HasNoOutAndNoOvertime()
        {
            var day = AttendanceEngine.Compute(new[] { T(10, 9, 30) }, new[] { General(10) }).Single();

            Assert.That(day.PunchIn, Is.EqualTo(T(10, 9, 30)));
            Assert.That(day.PunchOut, Is.Null);
            Assert.That(day.TotalHours, Is.Null);
            Assert.That(day.OvertimeHours, Is.EqualTo(0m));
        }

        [Test]
        public void SecondScanTooSoonAfterTheFirst_IsNotTreatedAsClockingOut()
        {
            var day = AttendanceEngine.Compute(new[] { T(10, 9, 30), T(10, 9, 40) }, new[] { General(10) }).Single();

            Assert.That(day.PunchOut, Is.Null, "10 minutes apart is a mistake, not a working day");
        }

        [Test]
        public void ManyScansThroughTheDay_UseFirstAsInAndLastAsOut()
        {
            var scans = new[] { T(10, 9, 30), T(10, 13, 0), T(10, 14, 0), T(10, 17, 30) };

            var day = AttendanceEngine.Compute(scans, new[] { General(10) }).Single();

            Assert.That(day.PunchIn, Is.EqualTo(T(10, 9, 30)));
            Assert.That(day.PunchOut, Is.EqualTo(T(10, 17, 30)));
            Assert.That(day.TotalHours, Is.EqualTo(8.00m), "breaks are not deducted; only first-to-last counts");
        }

        // ─── Overtime ────────────────────────────────────────────────────────

        [Test]
        public void Overtime_IsMeasuredAgainstTheRosteredShiftNotAFixedNumber()
        {
            // Morning shift is 6h. Working 08:00-15:00 is 7h => 1h overtime (a flat 9h rule would say none).
            var day = AttendanceEngine.Compute(new[] { T(10, 8, 0), T(10, 15, 0) }, new[] { Morning(10) }).Single();

            Assert.That(day.TotalHours, Is.EqualTo(7.00m));
            Assert.That(day.OvertimeHours, Is.EqualTo(1.00m));
        }

        [Test]
        public void LeavingEarly_IsNeverNegativeOvertime()
        {
            var day = AttendanceEngine.Compute(new[] { T(10, 9, 30), T(10, 13, 0) }, new[] { General(10) }).Single();

            Assert.That(day.TotalHours, Is.EqualTo(3.50m));
            Assert.That(day.OvertimeHours, Is.EqualTo(0m));
        }

        [Test]
        public void UnscheduledDay_UsesTheDefaultNineHourStandard()
        {
            var day = AttendanceEngine.Compute(new[] { T(10, 8, 0), T(10, 19, 0) }, NoRoster).Single();

            Assert.That(day.Unscheduled, Is.True);
            Assert.That(day.ShiftCode, Is.Null);
            Assert.That(day.Status, Is.EqualTo("PRESENT"), "no shift means nothing to be late for");
            Assert.That(day.TotalHours, Is.EqualTo(11.00m));
            Assert.That(day.OvertimeHours, Is.EqualTo(2.00m));
        }

        // ─── Which shift does a scan belong to? ──────────────────────────────

        [Test]
        public void ScanExactlyOnAShiftBoundary_IsTheEarlierShiftsClockOut()
        {
            var days = AttendanceEngine.Compute(new[] { T(10, 8, 0), T(10, 14, 0) }, new[] { Morning(10), Evening(10) });

            var day = days.Single();
            Assert.That(day.ShiftCode, Is.EqualTo("SFT_M"));
            Assert.That(day.PunchOut, Is.EqualTo(T(10, 14, 0)));
        }

        [Test]
        public void DoubleShiftWithAScanInEachShift_GivesTwoDays()
        {
            var scans = new[] { T(10, 7, 58), T(10, 13, 55), T(10, 14, 5), T(10, 20, 1) };

            var days = AttendanceEngine.Compute(scans, new[] { Morning(10), Evening(10) });

            Assert.That(days.Select(d => d.ShiftCode), Is.EqualTo(new[] { "SFT_M", "SFT_E" }));
        }

        [Test]
        public void ScanJustInsideTheEarlyWindow_CountsForTheShift_AndJustOutsideDoesNot()
        {
            // Morning starts 08:00; the window opens 3h earlier at 05:00.
            var inside = AttendanceEngine.Compute(new[] { T(10, 5, 0), T(10, 14, 0) }, new[] { Morning(10) }).Single();
            Assert.That(inside.Unscheduled, Is.False);

            var outside = AttendanceEngine.Compute(new[] { T(10, 4, 59) }, new[] { Morning(10) }).Single();
            Assert.That(outside.Unscheduled, Is.True, "too early to belong to that shift");
        }

        [Test]
        public void StayingLateForHandover_StillBelongsToTheShift()
        {
            // Morning ends 14:00; the window stays open 4h, until 18:00.
            var day = AttendanceEngine.Compute(new[] { T(10, 8, 0), T(10, 17, 30) }, new[] { Morning(10) }).Single();

            Assert.That(day.ShiftCode, Is.EqualTo("SFT_M"));
            Assert.That(day.PunchOut, Is.EqualTo(T(10, 17, 30)));
            Assert.That(day.OvertimeHours, Is.EqualTo(3.50m));
        }

        [Test]
        public void ScansOnDifferentDays_AreDifferentDaysEvenWithoutARoster()
        {
            var days = AttendanceEngine.Compute(new[] { T(10, 9, 0), T(10, 17, 0), T(11, 9, 5), T(11, 17, 5) }, NoRoster);

            Assert.That(days.Select(d => d.WorkDate), Is.EqualTo(new[] { D(10), D(11) }));
        }
    }
}
