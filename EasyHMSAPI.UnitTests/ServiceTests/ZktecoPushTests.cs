using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Application.Services.Implementations;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.ServiceTests
{
    [TestFixture]
    public class ZktecoAttlogParserTests
    {
        private static DateTime T(int hour, int minute, int second) => new(2026, 8, 10, hour, minute, second);

        [Test]
        public void TabSeparatedLine_GivesPinTimeStateAndVerify()
        {
            var result = ZktecoAttlogParser.Parse("12\t2026-08-10 09:05:10\t0\t1\t0\t0\t0");

            var punch = result.Punches.Single();
            Assert.That(punch.DeviceUserId, Is.EqualTo("12"));
            Assert.That(punch.PunchTime, Is.EqualTo(T(9, 5, 10)));
            Assert.That(punch.StateCode, Is.EqualTo(0));
            Assert.That(punch.VerifyType, Is.EqualTo(1));
            Assert.That(result.Skipped, Is.EqualTo(0));
        }

        [Test]
        public void SeveralLines_WithCrlfLfAndBlankLines_AreAllRead()
        {
            var body = "12\t2026-08-10 09:05:10\t0\t1\r\n13\t2026-08-10 09:06:00\t1\t1\n\r\n14\t2026-08-10 09:07:30\t0\t15\r\n";

            var result = ZktecoAttlogParser.Parse(body);

            Assert.That(result.Punches.Select(p => p.DeviceUserId), Is.EqualTo(new[] { "12", "13", "14" }));
            Assert.That(result.Skipped, Is.EqualTo(0));
        }

        [Test]
        public void ALineWithOnlyPinAndTime_IsStillAScan()
        {
            var punch = ZktecoAttlogParser.Parse("12\t2026-08-10 09:05:10").Punches.Single();

            Assert.That(punch.StateCode, Is.Null);
            Assert.That(punch.VerifyType, Is.Null);
        }

        [Test]
        public void PinsKeepTheirLeadingZeros()
        {
            Assert.That(ZktecoAttlogParser.Parse("0012\t2026-08-10 09:05:10\t0\t1").Punches.Single().DeviceUserId, Is.EqualTo("0012"));
        }

        [TestCase("12\t2026/08/10 09:05:10\t0\t1")]
        [TestCase("12\t2026-08-10T09:05:10\t0\t1")]
        public void OtherCommonDateFormats_AreAccepted(string line)
        {
            Assert.That(ZktecoAttlogParser.Parse(line).Punches.Single().PunchTime, Is.EqualTo(T(9, 5, 10)));
        }

        [Test]
        public void SpaceSeparatedLines_AreAcceptedAsAFallback()
        {
            var punch = ZktecoAttlogParser.Parse("12 2026-08-10 09:05:10 0 1").Punches.Single();

            Assert.That(punch.DeviceUserId, Is.EqualTo("12"));
            Assert.That(punch.PunchTime, Is.EqualTo(T(9, 5, 10)));
            Assert.That(punch.VerifyType, Is.EqualTo(1));
        }

        [Test]
        public void UnreadableLines_AreSkippedAndCounted_WithoutLosingTheGoodOnes()
        {
            var body = "12\t2026-08-10 09:05:10\t0\t1\nthis is garbage\n\t2026-08-10 09:06:00\t0\t1\n13\tnot-a-time\t0\t1\n14\t2026-08-10 09:07:30\t0\t1";

            var result = ZktecoAttlogParser.Parse(body);

            Assert.That(result.Punches.Select(p => p.DeviceUserId), Is.EqualTo(new[] { "12", "14" }));
            Assert.That(result.Skipped, Is.EqualTo(3));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   \r\n  ")]
        public void EmptyBodies_GiveNothingAndSkipNothing(string? body)
        {
            var result = ZktecoAttlogParser.Parse(body);

            Assert.That(result.Punches, Is.Empty);
            Assert.That(result.Skipped, Is.EqualTo(0));
        }

        [Test]
        public void EachScanKeepsItsRawLine()
        {
            var line = "12\t2026-08-10 09:05:10\t0\t1\t0";

            Assert.That(ZktecoAttlogParser.Parse(line).Punches.Single().RawLine, Is.EqualTo(line));
        }
    }

    [TestFixture]
    public class ZktecoPushServiceTests
    {
        private AppDbContext _context = null!;
        private ZktecoPushService _service = null!;
        private HrBiometricDevice _device = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _service = new ZktecoPushService(_context, new BiometricPunchIngestionService(_context), NullLogger<ZktecoPushService>.Instance);
            _device = new HrBiometricDevice { HospitalId = Guid.NewGuid(), Name = "Main gate", SerialNumber = "CJDE192360123", TokenHash = "hash" };
            _context.HrBiometricDevice.Add(_device);
            _context.SaveChanges();
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handshake_ForARegisteredDevice_SendsOptionsAndRecordsTheDeviceAsSeen()
        {
            var result = await _service.HandshakeAsync("CJDE192360123", "203.0.113.7", CancellationToken.None);

            Assert.That(result.StatusCode, Is.EqualTo(200));
            Assert.That(result.Body, Does.StartWith("GET OPTION FROM: CJDE192360123"));
            Assert.That(result.Body, Does.Contain("ATTLOGStamp=None"));
            Assert.That(result.Body, Does.Contain("Realtime=1"));
            var device = _context.HrBiometricDevice.Single();
            Assert.That(device.LastSeenAt, Is.Not.Null);
            Assert.That(device.LastSeenIp, Is.EqualTo("203.0.113.7"));
        }

        [Test]
        public async Task Handshake_IsCaseInsensitiveOnTheSerial()
        {
            var result = await _service.HandshakeAsync("cjde192360123", null, CancellationToken.None);

            Assert.That(result.StatusCode, Is.EqualTo(200));
        }

        [TestCase("NOSUCHDEVICE1")]
        [TestCase("")]
        [TestCase(null)]
        public async Task Handshake_ForAnUnregisteredSerial_IsRefused(string? serial)
        {
            var result = await _service.HandshakeAsync(serial, "203.0.113.7", CancellationToken.None);

            Assert.That(result.StatusCode, Is.EqualTo(403));
            Assert.That(_context.HrBiometricDevice.Count(), Is.EqualTo(1), "an unknown device must never be auto-registered");
        }

        [Test]
        public async Task Handshake_ForADeactivatedDevice_IsRefused()
        {
            _device.IsActive = false;
            await _context.SaveChangesAsync();

            var result = await _service.HandshakeAsync("CJDE192360123", null, CancellationToken.None);

            Assert.That(result.StatusCode, Is.EqualTo(403));
        }

        [Test]
        public async Task UploadingAttendanceLogs_StoresTheScansAndAcknowledgesTheCount()
        {
            var body = "12\t2026-08-10 09:05:10\t0\t1\n12\t2026-08-10 17:30:00\t1\t1\n";

            var result = await _service.UploadAsync("CJDE192360123", "ATTLOG", "555", body, "203.0.113.7", CancellationToken.None);

            Assert.That(result.StatusCode, Is.EqualTo(200));
            Assert.That(result.Body, Is.EqualTo("OK:2"));
            Assert.That(_context.HrBiometricPunch.Count(), Is.EqualTo(2));
            Assert.That(_context.HrBiometricPunch.All(p => p.HrBiometricDeviceId == _device.HrBiometricDeviceId && p.HospitalId == _device.HospitalId), Is.True);
        }

        [Test]
        public async Task TheStampFromAnUpload_IsHandedBackOnTheNextHandshake()
        {
            await _service.UploadAsync("CJDE192360123", "ATTLOG", "555", "12\t2026-08-10 09:05:10\t0\t1", null, CancellationToken.None);

            var handshake = await _service.HandshakeAsync("CJDE192360123", null, CancellationToken.None);

            Assert.That(handshake.Body, Does.Contain("ATTLOGStamp=555"), "so an offline device resumes instead of re-sending its history");
        }

        [Test]
        public async Task TheSameBatchSentTwice_IsAcknowledgedBothTimesButStoredOnce()
        {
            var body = "12\t2026-08-10 09:05:10\t0\t1\n12\t2026-08-10 17:30:00\t1\t1\n";

            var first = await _service.UploadAsync("CJDE192360123", "ATTLOG", "555", body, null, CancellationToken.None);
            var second = await _service.UploadAsync("CJDE192360123", "ATTLOG", "555", body, null, CancellationToken.None);

            Assert.That(first.Body, Is.EqualTo("OK:2"));
            Assert.That(second.StatusCode, Is.EqualTo(200), "an error would make the device re-send it forever");
            Assert.That(_context.HrBiometricPunch.Count(), Is.EqualTo(2));
        }

        [Test]
        public async Task ABatchWithSomeUnreadableLines_IsStillAcknowledged_AndTheGoodScansAreKept()
        {
            var body = "12\t2026-08-10 09:05:10\t0\t1\nsomething odd\n";

            var result = await _service.UploadAsync("CJDE192360123", "ATTLOG", null, body, null, CancellationToken.None);

            Assert.That(result.StatusCode, Is.EqualTo(200));
            Assert.That(_context.HrBiometricPunch.Count(), Is.EqualTo(1));
        }

        [TestCase("OPERLOG")]
        [TestCase("USERINFO")]
        [TestCase("ATTPHOTO")]
        [TestCase(null)]
        public async Task OtherTables_AreAcknowledgedAndIgnored(string? table)
        {
            var result = await _service.UploadAsync("CJDE192360123", table, null, "USER PIN=1\tName=Asha\tPri=0", null, CancellationToken.None);

            Assert.That(result.StatusCode, Is.EqualTo(200));
            Assert.That(result.Body, Is.EqualTo("OK"));
            Assert.That(_context.HrBiometricPunch.Any(), Is.False);
        }

        [Test]
        public async Task Upload_FromAnUnregisteredDevice_IsRefusedAndStoresNothing()
        {
            var result = await _service.UploadAsync("NOSUCHDEVICE1", "ATTLOG", null, "12\t2026-08-10 09:05:10\t0\t1", null, CancellationToken.None);

            Assert.That(result.StatusCode, Is.EqualTo(403));
            Assert.That(_context.HrBiometricPunch.Any(), Is.False);
        }

        [Test]
        public async Task Upload_FromADeactivatedDevice_IsRefusedAndStoresNothing()
        {
            _device.IsActive = false;
            await _context.SaveChangesAsync();

            var result = await _service.UploadAsync("CJDE192360123", "ATTLOG", null, "12\t2026-08-10 09:05:10\t0\t1", null, CancellationToken.None);

            Assert.That(result.StatusCode, Is.EqualTo(403));
            Assert.That(_context.HrBiometricPunch.Any(), Is.False);
        }

        [Test]
        public async Task CommandPolling_IsAcknowledgedAndCountsAsTheDeviceBeingAlive()
        {
            var result = await _service.TouchAsync("CJDE192360123", "203.0.113.7", CancellationToken.None);

            Assert.That(result.StatusCode, Is.EqualTo(200));
            Assert.That(result.Body, Is.EqualTo("OK"));
            Assert.That(_context.HrBiometricDevice.Single().LastSeenAt, Is.Not.Null);
        }

        [Test]
        public async Task ScansUploadedByThePushProtocol_BuildAttendanceForMappedPins()
        {
            var employee = new HrEmployee
            {
                HrEmployeeId = Guid.NewGuid(), HospitalId = _device.HospitalId, EmployeeCode = "EMP-2026-0001", FirstName = "Asha", LastName = "Tester",
                Gender = "Female", DateOfBirth = new DateOnly(1990, 1, 1), ContactNumber = "+91-9800000000", EmploymentType = "FULL_TIME_SALARIED",
                DepartmentId = Guid.NewGuid(), Designation = "Staff Nurse", DateOfJoining = new DateOnly(2020, 1, 1), PanNumber = "ABCDE1234F",
                PayrollTrack = "TRACK_A_SALARIED", IsActive = true, Status = "ACTIVE",
            };
            _context.HrEmployee.Add(employee);
            _context.HrEmployeeDeviceUser.Add(new HrEmployeeDeviceUser { HospitalId = _device.HospitalId, HrEmployeeId = employee.HrEmployeeId, DeviceUserId = "12" });
            await _context.SaveChangesAsync();

            await _service.UploadAsync("CJDE192360123", "ATTLOG", "1", "12\t2026-08-10 09:05:10\t0\t1\n12\t2026-08-10 17:30:00\t1\t1\n", null, CancellationToken.None);

            var log = _context.HrAttendanceLog.Single();
            Assert.That(log.HrEmployeeId, Is.EqualTo(employee.HrEmployeeId));
            Assert.That(log.PunchIn, Is.EqualTo(new DateTime(2026, 8, 10, 9, 5, 10)));
            Assert.That(log.PunchOut, Is.EqualTo(new DateTime(2026, 8, 10, 17, 30, 0)));
        }
    }
}
