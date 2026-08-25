using System;
using System.Collections.Generic;
using System.Linq;
using EasyHMSAPI.Application.Services;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.ServiceTests
{
    [TestFixture]
    public class PatientChurnAnalyzerTests
    {
        [Test]
        public void FindLapsedPatients_RegularVisitorOverdueBothThresholds_IsFlagged()
        {
            var today = DateTime.UtcNow.Date;
            // Visited every ~30 days for a year, but hasn't been back in 90 days --
            // well past the 60-day floor AND well past 1.5x their own ~30-day rhythm.
            var visits = Enumerable.Range(0, 6).Select(i => today.AddDays(-90 - i * 30)).ToList();
            var patients = new List<PatientVisitHistory> { new("P1", "Alice", true, visits) };

            var lapsed = PatientChurnAnalyzer.FindLapsedPatients(patients, today);

            var alice = lapsed.SingleOrDefault(l => l.PatientId == "P1");
            Assert.That(alice, Is.Not.Null, "A regular visitor overdue past both thresholds must be flagged as lapsed.");
            Assert.That(alice!.DaysSinceLastVisit, Is.EqualTo(90));
        }

        [Test]
        public void FindLapsedPatients_RegularVisitorStillWithinOwnRhythm_IsNotFlagged()
        {
            var today = DateTime.UtcNow.Date;
            // Visits every ~58 days; last visit was 65 days ago -- past the 60-day floor, but well
            // within 1.5x their own ~58-day rhythm (87 days), so this is normal for them.
            var visits = new List<DateTime> { today.AddDays(-65), today.AddDays(-123), today.AddDays(-181) };
            var patients = new List<PatientVisitHistory> { new("P2", "Bob", true, visits) };

            var lapsed = PatientChurnAnalyzer.FindLapsedPatients(patients, today);

            Assert.That(lapsed.Any(l => l.PatientId == "P2"), Is.False, "A patient still within their own normal visiting rhythm must not be flagged just for crossing the flat floor.");
        }

        [Test]
        public void FindLapsedPatients_OneTimeVisitor_IsNeverFlagged()
        {
            var today = DateTime.UtcNow.Date;
            var visits = new List<DateTime> { today.AddDays(-200) };
            var patients = new List<PatientVisitHistory> { new("P3", "Carol", true, visits) };

            var lapsed = PatientChurnAnalyzer.FindLapsedPatients(patients, today);

            Assert.That(lapsed.Any(l => l.PatientId == "P3"), Is.False, "A one-time visitor hasn't established a rhythm to have lapsed from.");
        }

        [Test]
        public void FindLapsedPatients_RecentRegularVisitor_IsNotFlagged()
        {
            var today = DateTime.UtcNow.Date;
            var visits = new List<DateTime> { today.AddDays(-10), today.AddDays(-40), today.AddDays(-70) };
            var patients = new List<PatientVisitHistory> { new("P4", "Dave", false, visits) };

            var lapsed = PatientChurnAnalyzer.FindLapsedPatients(patients, today);

            Assert.That(lapsed.Any(l => l.PatientId == "P4"), Is.False, "A patient seen 10 days ago is not lapsed regardless of marketing consent.");
        }
    }
}
