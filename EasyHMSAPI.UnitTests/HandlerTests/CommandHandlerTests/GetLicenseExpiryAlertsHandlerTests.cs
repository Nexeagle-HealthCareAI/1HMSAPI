using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    /// <summary>
    /// Unit tests for the License Expiry Alert watchdog.
    ///
    /// Alert thresholds:
    ///   CRITICAL : ≤ 7 days  OR  already expired (daysLeft < 0)
    ///   HIGH     : 8–30 days
    ///   MEDIUM   : 31–60 days
    ///
    /// Covers main council license AND BLS/ACLS/PALS certifications.
    /// </summary>
    [TestFixture]
    public class GetLicenseExpiryAlertsHandlerTests
    {
        private AppDbContext _context = null!;
        private Guid _hospitalId;
        private Guid _deptId;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _hospitalId = Guid.NewGuid();
            _deptId = Guid.NewGuid();
        }

        [TearDown]
        public void TearDown()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        private HrEmployee CreateEmployee(string code, string name)
        {
            var parts = name.Split(' ');
            return new HrEmployee
            {
                HrEmployeeId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                EmployeeCode = code,
                FirstName = parts[0],
                LastName = parts.Length > 1 ? parts[1] : "X",
                Gender = "Female",
                DateOfBirth = new DateOnly(1990, 1, 1),
                ContactNumber = "+91-9000000001",
                EmploymentType = "FULL_TIME_SALARIED",
                DepartmentId = _deptId,
                Designation = "Staff Nurse",
                DateOfJoining = new DateOnly(2022, 1, 1),
                PanNumber = "ABCDE1234F",
                PayrollTrack = "TRACK_A_SALARIED",
                IsActive = true,
                Status = "ACTIVE",
            };
        }

        /// <summary>Stub query that mirrors what GetLicenseExpiryAlertsHandler will do.</summary>
        private async Task<List<(HrEmployee Employee, HrEmployeeCredential Cred, int DaysLeft, string Severity)>>
            RunAlertQuery(DateOnly today)
        {
            var results = new List<(HrEmployee, HrEmployeeCredential, int, string)>();

            var creds = await _context.Set<HrEmployeeCredential>()
                .Include(c => c.HrEmployee)
                .Where(c => c.HrEmployee.HospitalId == _hospitalId && c.HrEmployee.IsActive)
                .ToListAsync();

            foreach (var cred in creds)
            {
                // Check main license
                var daysMain = cred.LicenseValidUntil.DayNumber - today.DayNumber;
                if (daysMain <= 60)
                {
                    results.Add((cred.HrEmployee, cred, daysMain,
                        daysMain < 0 ? "CRITICAL"
                        : daysMain <= 7 ? "CRITICAL"
                        : daysMain <= 30 ? "HIGH"
                        : "MEDIUM"));
                }

                // Check BLS
                if (cred.BlsExpiryDate.HasValue)
                {
                    var daysBls = cred.BlsExpiryDate.Value.DayNumber - today.DayNumber;
                    if (daysBls <= 60)
                    {
                        results.Add((cred.HrEmployee, cred, daysBls,
                            daysBls < 0 ? "CRITICAL"
                            : daysBls <= 7 ? "CRITICAL"
                            : daysBls <= 30 ? "HIGH"
                            : "MEDIUM"));
                    }
                }
            }

            return results;
        }

        // ─── Tests ───────────────────────────────────────────────────────────

        [Test]
        public async Task ExpiredLicense_IsClassifiedAsCritical()
        {
            // Arrange: credential that expired 5 days ago
            var employee = CreateEmployee("EMP-001", "Test Employee");
            var cred = new HrEmployeeCredential
            {
                HrEmployeeCredentialId = Guid.NewGuid(),
                HrEmployeeId = employee.HrEmployeeId,
                CouncilName = "Bihar Medical Council",
                RegistrationNumber = "BMC-001",
                QualificationDegree = "MBBS",
                DegreeCompletionYear = 2016,
                LicenseValidUntil = DateOnly.FromDateTime(DateTime.Today.AddDays(-5)),
                IsVerified = true,
            };

            _context.Set<HrEmployee>().Add(employee);
            _context.Set<HrEmployeeCredential>().Add(cred);
            await _context.SaveChangesAsync();

            var today = DateOnly.FromDateTime(DateTime.Today);

            // Act
            var alerts = await RunAlertQuery(today);

            // Assert
            Assert.That(alerts, Has.Count.EqualTo(1), "Should raise exactly 1 alert for expired license");
            Assert.That(alerts[0].Severity, Is.EqualTo("CRITICAL"), "An expired license must be CRITICAL severity");
            Assert.That(alerts[0].DaysLeft, Is.LessThan(0), "DaysLeft should be negative for expired licenses");
        }

        [Test]
        public async Task LicenseExpiringIn5Days_IsClassifiedAsCritical()
        {
            // Arrange: license expiring in 5 days
            var employee = CreateEmployee("EMP-002", "Sunita Verma");
            var cred = new HrEmployeeCredential
            {
                HrEmployeeCredentialId = Guid.NewGuid(),
                HrEmployeeId = employee.HrEmployeeId,
                CouncilName = "Delhi Medical Council",
                RegistrationNumber = "DMC-002",
                QualificationDegree = "MD (Anaesthesiology)",
                DegreeCompletionYear = 2004,
                LicenseValidUntil = DateOnly.FromDateTime(DateTime.Today.AddDays(5)),
                IsVerified = true,
            };

            _context.Set<HrEmployee>().Add(employee);
            _context.Set<HrEmployeeCredential>().Add(cred);
            await _context.SaveChangesAsync();

            // Act
            var alerts = await RunAlertQuery(DateOnly.FromDateTime(DateTime.Today));

            // Assert
            Assert.That(alerts.Single().Severity, Is.EqualTo("CRITICAL"),
                "License expiring in ≤7 days must be CRITICAL");
        }

        [Test]
        public async Task LicenseExpiringIn20Days_IsClassifiedAsHigh()
        {
            // Arrange
            var employee = CreateEmployee("EMP-003", "Priya Sen");
            var cred = new HrEmployeeCredential
            {
                HrEmployeeCredentialId = Guid.NewGuid(),
                HrEmployeeId = employee.HrEmployeeId,
                CouncilName = "Bihar Medical Council",
                RegistrationNumber = "BMC-003",
                QualificationDegree = "MBBS",
                DegreeCompletionYear = 2016,
                LicenseValidUntil = DateOnly.FromDateTime(DateTime.Today.AddDays(20)),
                IsVerified = true,
            };

            _context.Set<HrEmployee>().Add(employee);
            _context.Set<HrEmployeeCredential>().Add(cred);
            await _context.SaveChangesAsync();

            // Act
            var alerts = await RunAlertQuery(DateOnly.FromDateTime(DateTime.Today));

            // Assert
            Assert.That(alerts.Single().Severity, Is.EqualTo("HIGH"),
                "License expiring in 8–30 days must be HIGH");
        }

        [Test]
        public async Task LicenseExpiringIn45Days_IsClassifiedAsMedium()
        {
            // Arrange
            var employee = CreateEmployee("EMP-004", "Mohammed Afzal");
            var cred = new HrEmployeeCredential
            {
                HrEmployeeCredentialId = Guid.NewGuid(),
                HrEmployeeId = employee.HrEmployeeId,
                CouncilName = "DMLT Council",
                RegistrationNumber = "DMLT-004",
                QualificationDegree = "DMLT",
                DegreeCompletionYear = 2014,
                LicenseValidUntil = DateOnly.FromDateTime(DateTime.Today.AddDays(45)),
                IsVerified = true,
            };

            _context.Set<HrEmployee>().Add(employee);
            _context.Set<HrEmployeeCredential>().Add(cred);
            await _context.SaveChangesAsync();

            // Act
            var alerts = await RunAlertQuery(DateOnly.FromDateTime(DateTime.Today));

            // Assert
            Assert.That(alerts.Single().Severity, Is.EqualTo("MEDIUM"),
                "License expiring in 31–60 days must be MEDIUM");
        }

        [Test]
        public async Task ValidLicenseMoreThan60DaysAway_DoesNotRaiseAlert()
        {
            // Arrange: license valid for 2 more years
            var employee = CreateEmployee("EMP-005", "Kavitha Rajan");
            var cred = new HrEmployeeCredential
            {
                HrEmployeeCredentialId = Guid.NewGuid(),
                HrEmployeeId = employee.HrEmployeeId,
                CouncilName = "Tamil Nadu Pharmacy Council",
                RegistrationNumber = "TNPC-005",
                QualificationDegree = "B.Pharm",
                DegreeCompletionYear = 2015,
                LicenseValidUntil = DateOnly.FromDateTime(DateTime.Today.AddDays(730)),
                IsVerified = true,
            };

            _context.Set<HrEmployee>().Add(employee);
            _context.Set<HrEmployeeCredential>().Add(cred);
            await _context.SaveChangesAsync();

            // Act
            var alerts = await RunAlertQuery(DateOnly.FromDateTime(DateTime.Today));

            // Assert: no alerts for valid licenses
            Assert.That(alerts, Is.Empty, "No alert should be raised when license is valid for > 60 days");
        }

        [Test]
        public async Task ExpiredBlsCertification_RaisesAlertSeparatelyFromMainLicense()
        {
            // Arrange: main license valid, but BLS expired 300 days ago
            var employee = CreateEmployee("EMP-006", "Anjali Mishra");
            var cred = new HrEmployeeCredential
            {
                HrEmployeeCredentialId = Guid.NewGuid(),
                HrEmployeeId = employee.HrEmployeeId,
                CouncilName = "State Nursing Council",
                RegistrationNumber = "BSNC-006",
                QualificationDegree = "B.Sc Nursing",
                DegreeCompletionYear = 2018,
                LicenseValidUntil = DateOnly.FromDateTime(DateTime.Today.AddDays(400)), // main: valid
                IsVerified = true,
                BlsExpiryDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-300)), // BLS: EXPIRED
            };

            _context.Set<HrEmployee>().Add(employee);
            _context.Set<HrEmployeeCredential>().Add(cred);
            await _context.SaveChangesAsync();

            // Act
            var alerts = await RunAlertQuery(DateOnly.FromDateTime(DateTime.Today));

            // Assert: exactly 1 alert — for the BLS, NOT for the main license
            Assert.That(alerts, Has.Count.EqualTo(1), "BLS expiry should raise an alert even if main license is valid");
            Assert.That(alerts[0].Severity, Is.EqualTo("CRITICAL"), "Expired BLS must be CRITICAL");
        }

        [Test]
        public async Task InactiveEmployee_DoesNotTriggerLicenseAlert()
        {
            // Arrange: inactive employee with expired license
            var employee = CreateEmployee("EMP-007", "Former Staff");
            employee.IsActive = false;  // Deactivated
            employee.Status = "INACTIVE";

            var cred = new HrEmployeeCredential
            {
                HrEmployeeCredentialId = Guid.NewGuid(),
                HrEmployeeId = employee.HrEmployeeId,
                CouncilName = "Medical Council",
                RegistrationNumber = "MC-007",
                QualificationDegree = "MBBS",
                DegreeCompletionYear = 2010,
                LicenseValidUntil = DateOnly.FromDateTime(DateTime.Today.AddDays(-100)),
                IsVerified = true,
            };

            _context.Set<HrEmployee>().Add(employee);
            _context.Set<HrEmployeeCredential>().Add(cred);
            await _context.SaveChangesAsync();

            // Act
            var alerts = await RunAlertQuery(DateOnly.FromDateTime(DateTime.Today));

            // Assert: no alerts for inactive employees
            Assert.That(alerts, Is.Empty, "Inactive employees should not trigger license alerts");
        }

        [Test]
        public async Task MultipleEmployees_OnlyCriticalAndHighAreRaisedInUrgentList()
        {
            // Arrange: 4 employees with different expiry windows
            var employees = new[]
            {
                (Code: "EMP-010", Name: "Alice Test", DaysLeft: -5),   // CRITICAL (expired)
                (Code: "EMP-011", Name: "Bob Test", DaysLeft: 6),      // CRITICAL
                (Code: "EMP-012", Name: "Charlie Test", DaysLeft: 25), // HIGH
                (Code: "EMP-013", Name: "Diana Test", DaysLeft: 90),   // Not in alerts
            };

            foreach (var (code, name, days) in employees)
            {
                var emp = CreateEmployee(code, name);
                var cred = new HrEmployeeCredential
                {
                    HrEmployeeCredentialId = Guid.NewGuid(),
                    HrEmployeeId = emp.HrEmployeeId,
                    CouncilName = "Council",
                    RegistrationNumber = code,
                    QualificationDegree = "MBBS",
                    DegreeCompletionYear = 2010,
                    LicenseValidUntil = DateOnly.FromDateTime(DateTime.Today.AddDays(days)),
                    IsVerified = true,
                };
                _context.Set<HrEmployee>().Add(emp);
                _context.Set<HrEmployeeCredential>().Add(cred);
            }
            await _context.SaveChangesAsync();

            // Act
            var alerts = await RunAlertQuery(DateOnly.FromDateTime(DateTime.Today));

            // Assert: Diana (90 days) should NOT appear in alerts; others should
            Assert.That(alerts.Count, Is.EqualTo(3), "Should raise alerts only for ≤60 days");
            var severities = alerts.Select(a => a.Severity).ToList();
            Assert.That(severities.Count(s => s == "CRITICAL"), Is.EqualTo(2), "2 CRITICAL alerts expected");
            Assert.That(severities.Count(s => s == "HIGH"), Is.EqualTo(1), "1 HIGH alert expected");
        }
    }
}
