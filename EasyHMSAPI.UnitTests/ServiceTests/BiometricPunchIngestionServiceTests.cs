using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Services.Implementations;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.ServiceTests
{
    [TestFixture]
    public class BiometricPunchIngestionServiceTests
    {
        private AppDbContext _context = null!;
        private BiometricPunchIngestionService _service = null!;
        private Guid _hospital;
        private HrBiometricDevice _device = null!;
        private HrEmployee _asha = null!;

        private static DateTime T(int day, int hour, int minute, int second = 0) => new(2026, 8, day, hour, minute, second);
        private static DateOnly D(int day) => new(2026, 8, day);

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _service = new BiometricPunchIngestionService(_context);
            _hospital = Guid.NewGuid();
            _device = NewDevice(_hospital, "ZK-001");
            _asha = NewEmployee(_hospital, "EMP-2026-0001");
            _context.HrBiometricDevice.Add(_device);
            _context.HrEmployee.Add(_asha);
            _context.HrEmployeeDeviceUser.Add(new HrEmployeeDeviceUser { HospitalId = _hospital, HrEmployeeId = _asha.HrEmployeeId, DeviceUserId = "12" });
            _context.SaveChanges();
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        // ─── Building attendance ─────────────────────────────────────────────

        [Test]
        public async Task MappedScans_BuildTheDaysAttendance()
        {
            RosterGeneralShift(_asha, 10);

            var result = await Ingest(P("12", T(10, 9, 28)), P("12", T(10, 17, 31)));

            Assert.That(result.Accepted, Is.EqualTo(2));
            Assert.That(result.AttendanceDaysUpdated, Is.EqualTo(1));
            var log = _context.HrAttendanceLog.Single();
            Assert.That(log.HrEmployeeId, Is.EqualTo(_asha.HrEmployeeId));
            Assert.That(log.AttendanceDate, Is.EqualTo(D(10)));
            Assert.That(log.PunchIn, Is.EqualTo(T(10, 9, 28)));
            Assert.That(log.PunchOut, Is.EqualTo(T(10, 17, 31)));
            Assert.That(log.Status, Is.EqualTo("PRESENT"));
            Assert.That(log.PunchSource, Is.EqualTo("BIOMETRIC"));
            Assert.That(log.BiometricDeviceId, Is.EqualTo("ZK-001"));
            Assert.That(log.Notes, Is.Null);
        }

        [Test]
        public async Task ResendingTheSameBatch_StoresEachScanOnceAndKeepsOneAttendanceRow()
        {
            RosterGeneralShift(_asha, 10);
            var batch = new[] { P("12", T(10, 9, 28)), P("12", T(10, 17, 31)) };

            var first = await Ingest(batch);
            var second = await Ingest(batch);

            Assert.That(first.Accepted, Is.EqualTo(2));
            Assert.That(second.Accepted, Is.EqualTo(0));
            Assert.That(second.Duplicates, Is.EqualTo(2));
            Assert.That(_context.HrBiometricPunch.Count(), Is.EqualTo(2));
            Assert.That(_context.HrAttendanceLog.Count(), Is.EqualTo(1));
        }

        [Test]
        public async Task ScanWithFractionalSeconds_IsTheSameScanAsTheWholeSecondVersion()
        {
            await Ingest(P("12", T(10, 9, 28)));

            var again = await Ingest(P("12", T(10, 9, 28).AddMilliseconds(400)));

            Assert.That(again.Duplicates, Is.EqualTo(1));
            Assert.That(_context.HrBiometricPunch.Count(), Is.EqualTo(1));
        }

        [Test]
        public async Task RepeatsInsideOneBatch_AreCountedOnce()
        {
            var result = await Ingest(P("12", T(10, 9, 28)), P("12", T(10, 9, 28)), P("12", T(10, 17, 31)));

            Assert.That(result.Accepted, Is.EqualTo(2));
            Assert.That(result.Duplicates, Is.EqualTo(1));
        }

        [Test]
        public async Task TheOutScanArrivingInALaterBatch_CompletesTheDay()
        {
            RosterGeneralShift(_asha, 10);

            await Ingest(P("12", T(10, 9, 28)));
            Assert.That(_context.HrAttendanceLog.Single().PunchOut, Is.Null, "only the IN has arrived so far");

            await Ingest(P("12", T(10, 17, 31)));

            var log = _context.HrAttendanceLog.Single();
            Assert.That(log.PunchOut, Is.EqualTo(T(10, 17, 31)));
            Assert.That(log.TotalHoursWorked, Is.EqualTo(8.05m));
        }

        [Test]
        public async Task ScansArrivingOutOfOrder_GiveTheSameDayAsInOrder()
        {
            RosterGeneralShift(_asha, 10);

            await Ingest(P("12", T(10, 17, 31)));     // the OUT arrives first
            await Ingest(P("12", T(10, 9, 28)));      // then the IN

            var log = _context.HrAttendanceLog.Single();
            Assert.That(log.PunchIn, Is.EqualTo(T(10, 9, 28)));
            Assert.That(log.PunchOut, Is.EqualTo(T(10, 17, 31)));
        }

        [Test]
        public async Task NightShift_IsOneRowOnTheDayItStarted()
        {
            RosterShift(_asha, 10, "SFT_N", new TimeOnly(20, 0), new TimeOnly(8, 0), 30);

            await Ingest(P("12", T(10, 19, 55)), P("12", T(11, 8, 10)));

            var log = _context.HrAttendanceLog.Single();
            Assert.That(log.AttendanceDate, Is.EqualTo(D(10)));
            Assert.That(log.PunchIn, Is.EqualTo(T(10, 19, 55)));
            Assert.That(log.PunchOut, Is.EqualTo(T(11, 8, 10)));
            Assert.That(log.TotalHoursWorked, Is.EqualTo(12.25m));
        }

        [Test]
        public async Task LateArrival_IsMarkedLate()
        {
            RosterGeneralShift(_asha, 10);

            await Ingest(P("12", T(10, 9, 50)), P("12", T(10, 17, 30)));

            Assert.That(_context.HrAttendanceLog.Single().Status, Is.EqualTo("LATE"));
        }

        [Test]
        public async Task ScansWithNoRoster_AreFlaggedUnscheduled_AndTheFlagClearsOnceARosterExists()
        {
            await Ingest(P("12", T(10, 9, 30)), P("12", T(10, 17, 30)));
            Assert.That(_context.HrAttendanceLog.Single().Notes, Is.EqualTo("UNSCHEDULED"));

            RosterGeneralShift(_asha, 10);
            await _service.RebuildAsync(_hospital, _asha.HrEmployeeId, D(10), D(10), CancellationToken.None);

            Assert.That(_context.HrAttendanceLog.Single().Notes, Is.Null);
        }

        [Test]
        public async Task ATypedNoteOnTheDay_IsNeverOverwrittenByTheUnscheduledFlag()
        {
            _context.HrAttendanceLog.Add(new HrAttendanceLog
            {
                HrEmployeeId = _asha.HrEmployeeId, AttendanceDate = D(10), Status = "PRESENT", PunchSource = "BIOMETRIC", Notes = "Covering for Bikram",
            });
            await _context.SaveChangesAsync();

            await Ingest(P("12", T(10, 9, 30)), P("12", T(10, 17, 30)));

            Assert.That(_context.HrAttendanceLog.Single().Notes, Is.EqualTo("Covering for Bikram"));
        }

        // ─── A person's decision beats a device scan ─────────────────────────

        [Test]
        public async Task ManuallyCorrectedDay_IsNotOverwrittenByDeviceScans()
        {
            _context.HrAttendanceLog.Add(new HrAttendanceLog
            {
                HrEmployeeId = _asha.HrEmployeeId, AttendanceDate = D(10), PunchIn = T(10, 8, 0), PunchOut = T(10, 16, 0),
                Status = "HALF_DAY", PunchSource = "MANUAL_OVERRIDE", Notes = "Corrected by HR",
            });
            await _context.SaveChangesAsync();

            await Ingest(P("12", T(10, 9, 30)), P("12", T(10, 17, 30)));

            var log = _context.HrAttendanceLog.Single();
            Assert.That(log.Status, Is.EqualTo("HALF_DAY"));
            Assert.That(log.PunchIn, Is.EqualTo(T(10, 8, 0)));
            Assert.That(log.PunchSource, Is.EqualTo("MANUAL_OVERRIDE"));
        }

        [Test]
        public async Task DayOnApprovedLeave_IsNotOverwrittenByDeviceScans()
        {
            _context.HrAttendanceLog.Add(new HrAttendanceLog { HrEmployeeId = _asha.HrEmployeeId, AttendanceDate = D(10), Status = "ON_LEAVE", PunchSource = "MANUAL_OVERRIDE" });
            await _context.SaveChangesAsync();

            await Ingest(P("12", T(10, 9, 30)), P("12", T(10, 17, 30)));

            Assert.That(_context.HrAttendanceLog.Single().Status, Is.EqualTo("ON_LEAVE"));
        }

        // ─── PIN mapping ─────────────────────────────────────────────────────

        [Test]
        public async Task UnmappedPin_IsStoredButBuildsNoAttendance_ThenMappingItBuildsTheDay()
        {
            var result = await Ingest(P("77", T(10, 9, 30)), P("77", T(10, 17, 30)));

            Assert.That(result.Accepted, Is.EqualTo(2));
            Assert.That(result.Unmapped, Is.EqualTo(2));
            Assert.That(_context.HrAttendanceLog.Any(), Is.False);
            Assert.That(_context.HrBiometricPunch.All(p => p.HrEmployeeId == null), Is.True);

            // HR then maps PIN 77 to an employee.
            var bikram = NewEmployee(_hospital, "EMP-2026-0002");
            _context.HrEmployee.Add(bikram);
            _context.HrEmployeeDeviceUser.Add(new HrEmployeeDeviceUser { HospitalId = _hospital, HrEmployeeId = bikram.HrEmployeeId, DeviceUserId = "77" });
            await _context.SaveChangesAsync();

            var updated = await _service.LinkPinAndRebuildAsync(_hospital, "77", bikram.HrEmployeeId, CancellationToken.None);

            Assert.That(updated, Is.EqualTo(1));
            var log = _context.HrAttendanceLog.Single();
            Assert.That(log.HrEmployeeId, Is.EqualTo(bikram.HrEmployeeId));
            Assert.That(log.PunchIn, Is.EqualTo(T(10, 9, 30)));
            Assert.That(log.PunchOut, Is.EqualTo(T(10, 17, 30)));
            Assert.That(_context.HrBiometricPunch.All(p => p.HrEmployeeId == bikram.HrEmployeeId), Is.True);
        }

        [Test]
        public async Task APinMappedAtAnotherHospital_IsNotUsed()
        {
            // Different hospitals reuse the same PINs. PIN 5 belongs to someone at hospital B, not to anyone here.
            var otherHospital = Guid.NewGuid();
            var stranger = NewEmployee(otherHospital, "EMP-2026-0001");
            _context.HrEmployee.Add(stranger);
            _context.HrEmployeeDeviceUser.Add(new HrEmployeeDeviceUser { HospitalId = otherHospital, HrEmployeeId = stranger.HrEmployeeId, DeviceUserId = "5" });
            await _context.SaveChangesAsync();

            var result = await Ingest(P("5", T(10, 9, 30)), P("5", T(10, 17, 30)));

            Assert.That(result.Unmapped, Is.EqualTo(2), "PIN 5 means nothing at this hospital");
            Assert.That(_context.HrAttendanceLog.Any(), Is.False, "the other hospital's employee must not get attendance from this device");
        }

        // ─── Bad input, device health ────────────────────────────────────────

        [Test]
        public async Task UnusableRecords_AreCountedAndSkipped()
        {
            var result = await Ingest(
                P("", T(10, 9, 30)),                                        // no PIN
                P("   ", T(10, 9, 30)),                                     // blank PIN
                P("12", new DateTime(2000, 1, 1, 0, 0, 0)),                 // dead-battery clock
                P("12", DateTime.UtcNow.AddDays(30)),                       // far future
                P(new string('9', 51), T(10, 9, 30)));                      // PIN longer than the column

            Assert.That(result.Received, Is.EqualTo(5));
            Assert.That(result.Invalid, Is.EqualTo(5));
            Assert.That(result.Accepted, Is.EqualTo(0));
            Assert.That(_context.HrBiometricPunch.Any(), Is.False);
        }

        [Test]
        public async Task IngestingRecordsTheDeviceAsSeen()
        {
            await _service.IngestAsync(_device, new[] { P("12", T(10, 9, 30)), P("12", T(10, 17, 30)) }, "203.0.113.7", CancellationToken.None);

            var device = _context.HrBiometricDevice.Single();
            Assert.That(device.LastSeenAt, Is.Not.Null);
            Assert.That(device.LastSeenIp, Is.EqualTo("203.0.113.7"));
            Assert.That(device.LastPunchTime, Is.EqualTo(T(10, 17, 30)));
        }

        [Test]
        public async Task AnEmptyBatch_StillRecordsTheDeviceAsSeen()
        {
            // A device with nothing new to send still "checks in"; that is how we know it is online.
            var result = await Ingest();

            Assert.That(result.Received, Is.EqualTo(0));
            Assert.That(_context.HrBiometricDevice.Single().LastSeenAt, Is.Not.Null);
        }

        // ─── helpers ─────────────────────────────────────────────────────────

        private Task<PunchIngestionResult> Ingest(params IncomingPunch[] punches) =>
            _service.IngestAsync(_device, punches, null, CancellationToken.None);

        private static IncomingPunch P(string pin, DateTime time) => new(pin, time);

        private static HrBiometricDevice NewDevice(Guid hospitalId, string serial) => new()
        {
            HospitalId = hospitalId, Name = "Main gate", SerialNumber = serial, Model = "K40 Pro", TokenHash = "hash",
        };

        private static HrEmployee NewEmployee(Guid hospitalId, string code) => new()
        {
            HrEmployeeId = Guid.NewGuid(),
            HospitalId = hospitalId,
            EmployeeCode = code,
            FirstName = "Test",
            LastName = "Employee",
            Gender = "Female",
            DateOfBirth = new DateOnly(1990, 1, 1),
            ContactNumber = "+91-9800000000",
            EmploymentType = "FULL_TIME_SALARIED",
            DepartmentId = Guid.NewGuid(),
            Designation = "Staff Nurse",
            DateOfJoining = new DateOnly(2020, 1, 1),
            PanNumber = "ABCDE1234F",
            PayrollTrack = "TRACK_A_SALARIED",
            IsActive = true,
            Status = "ACTIVE",
        };

        private void RosterGeneralShift(HrEmployee employee, int day) =>
            RosterShift(employee, day, "SFT_G", new TimeOnly(9, 30), new TimeOnly(17, 30), 10);

        private void RosterShift(HrEmployee employee, int day, string code, TimeOnly start, TimeOnly end, int grace)
        {
            var shift = new HrHospitalShift
            {
                HrHospitalShiftId = Guid.NewGuid(), HospitalId = employee.HospitalId, ShiftCode = code, ShiftName = code,
                StartTime = start, EndTime = end, GracePeriodMinutes = grace,
            };
            _context.HrHospitalShift.Add(shift);
            _context.HrDutyRoster.Add(new HrDutyRoster
            {
                HrDutyRosterId = Guid.NewGuid(), HospitalId = employee.HospitalId, HrEmployeeId = employee.HrEmployeeId,
                HrHospitalShiftId = shift.HrHospitalShiftId, RosterDate = D(day),
            });
            _context.SaveChanges();
        }
    }
}
