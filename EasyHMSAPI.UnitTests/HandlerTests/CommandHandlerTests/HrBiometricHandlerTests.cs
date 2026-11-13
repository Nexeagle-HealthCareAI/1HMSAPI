using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Application.Services.Implementations;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    /// <summary>
    /// Device registry, device authentication and PIN mapping. The recurring theme is isolation: a device
    /// belongs to one hospital, a PIN means something only inside its hospital, and a manager at one
    /// hospital can't touch another's devices or staff.
    /// </summary>
    [TestFixture]
    public class HrBiometricHandlerTests
    {
        private const string ManageEmployees = "hr.manage_employees";

        private AppDbContext _context = null!;
        private BiometricPunchIngestionService _ingestion = null!;
        private Guid _hospitalA;
        private Guid _hospitalB;
        private Guid _managerA;
        private Guid _managerB;
        private Guid _plainMemberA;
        private HrEmployee _asha = null!;

        private static DateTime T(int day, int hour, int minute) => new(2026, 8, day, hour, minute, 0);

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _ingestion = new BiometricPunchIngestionService(_context);
            _hospitalA = Guid.NewGuid();
            _hospitalB = Guid.NewGuid();
            _managerA = HrAuthSeed.SeedMember(_context, _hospitalA, ManageEmployees);
            _managerB = HrAuthSeed.SeedMember(_context, _hospitalB, ManageEmployees);
            _plainMemberA = HrAuthSeed.SeedMember(_context, _hospitalA);
            _asha = NewEmployee(_hospitalA, "EMP-2026-0001", "Asha");
            _context.HrEmployee.Add(_asha);
            _context.SaveChanges();
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        // ─── Registering a device ────────────────────────────────────────────

        [Test]
        public async Task Register_ByAManagerAtTheHospital_CreatesTheDeviceAndShowsTheTokenOnce()
        {
            var result = await Register(_managerA, _hospitalA, "Main gate", "cjde192360123");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Token, Is.Not.Null.And.Not.Empty);
            Assert.That(result.Device!.SerialNumber, Is.EqualTo("CJDE192360123"), "stored upper-cased");

            var stored = _context.HrBiometricDevice.Single();
            Assert.That(stored.HospitalId, Is.EqualTo(_hospitalA));
            Assert.That(stored.TokenHash, Is.Not.EqualTo(result.Token), "only a hash is kept, never the token itself");
            Assert.That(BiometricDeviceCredentials.Matches(result.Token, stored.TokenHash), Is.True);
        }

        [Test]
        public async Task Register_ASerialThatIsAlreadyRegistered_IsRefused_WithoutSayingWhoHasIt()
        {
            await Register(_managerA, _hospitalA, "Main gate", "CJDE192360123");

            var again = await Register(_managerB, _hospitalB, "Front desk", "cjde192360123");

            Assert.That(again.Success, Is.False);
            Assert.That(again.Message, Is.EqualTo("That serial number is already registered."));
            Assert.That(_context.HrBiometricDevice.Count(), Is.EqualTo(1));
        }

        [Test]
        public async Task Register_ByAManagerOfAnotherHospital_IsRefused()
        {
            var result = await Register(_managerB, _hospitalA, "Main gate", "CJDE192360123");

            Assert.That(result.Success, Is.False);
            Assert.That(_context.HrBiometricDevice.Any(), Is.False);
        }

        [Test]
        public async Task Register_ByAMemberWithoutTheHrPermission_IsRefused()
        {
            var result = await Register(_plainMemberA, _hospitalA, "Main gate", "CJDE192360123");

            Assert.That(result.Success, Is.False);
            Assert.That(_context.HrBiometricDevice.Any(), Is.False);
        }

        [TestCase("", "CJDE192360123")]
        [TestCase("   ", "CJDE192360123")]
        [TestCase("Main gate", "")]
        [TestCase("Main gate", "ab")]
        [TestCase("Main gate", "has space")]
        [TestCase("Main gate", "bad;chars")]
        public async Task Register_MissingOrMalformedDetails_AreRefused(string name, string serial)
        {
            var result = await Register(_managerA, _hospitalA, name, serial);

            Assert.That(result.Success, Is.False);
            Assert.That(_context.HrBiometricDevice.Any(), Is.False);
        }

        // ─── A device delivering scans ───────────────────────────────────────

        [Test]
        public async Task Ingest_WithTheRightSerialAndToken_StoresTheScans()
        {
            var (serial, token) = await RegisteredDevice();

            var result = await Ingest(serial, token, ("12", T(10, 9, 30)), ("12", T(10, 17, 30)));

            Assert.That(result.Success, Is.True);
            Assert.That(result.Accepted, Is.EqualTo(2));
            Assert.That(_context.HrBiometricPunch.Count(), Is.EqualTo(2));
        }

        [Test]
        public async Task Ingest_SerialIsCaseInsensitive()
        {
            var (serial, token) = await RegisteredDevice();

            var result = await Ingest(serial.ToLowerInvariant(), token, ("12", T(10, 9, 30)));

            Assert.That(result.Success, Is.True);
        }

        [Test]
        public async Task Ingest_WithAWrongToken_IsUnauthorizedAndStoresNothing()
        {
            var (serial, _) = await RegisteredDevice();

            var result = await Ingest(serial, "not-the-token", ("12", T(10, 9, 30)));

            Assert.That(result.Unauthorized, Is.True);
            Assert.That(result.Success, Is.False);
            Assert.That(_context.HrBiometricPunch.Any(), Is.False);
        }

        [Test]
        public async Task Ingest_UnknownSerialAndWrongToken_GetTheSameAnswer_SoSerialsCantBeProbed()
        {
            var (serial, _) = await RegisteredDevice();

            var wrongToken = await Ingest(serial, "not-the-token", ("12", T(10, 9, 30)));
            var unknownSerial = await Ingest("NOSUCHDEVICE1", "not-the-token", ("12", T(10, 9, 30)));

            Assert.That(unknownSerial.Unauthorized, Is.True);
            Assert.That(unknownSerial.Message, Is.EqualTo(wrongToken.Message));
        }

        [TestCase(null, null)]
        [TestCase("", "")]
        public async Task Ingest_WithNoCredentials_IsUnauthorized(string? serial, string? token)
        {
            await RegisteredDevice();

            var result = await Ingest(serial, token, ("12", T(10, 9, 30)));

            Assert.That(result.Unauthorized, Is.True);
        }

        [Test]
        public async Task Ingest_FromADeactivatedDevice_IsRejectedEvenWithTheRightToken()
        {
            var (serial, token) = await RegisteredDevice();
            var deviceId = _context.HrBiometricDevice.Single().HrBiometricDeviceId;
            await new SetHrBiometricDeviceActiveHandler(_context).Handle(
                new SetHrBiometricDeviceActiveRequestModel { HrBiometricDeviceId = deviceId, IsActive = false, LoggedInUserId = _managerA }, CancellationToken.None);

            var result = await Ingest(serial, token, ("12", T(10, 9, 30)));

            Assert.That(result.Unauthorized, Is.True);
            Assert.That(_context.HrBiometricPunch.Any(), Is.False);
        }

        [Test]
        public async Task Ingest_AnOversizedBatch_IsRefusedWithoutStoringAnything()
        {
            var (serial, token) = await RegisteredDevice();
            var tooMany = Enumerable.Range(0, IngestBiometricPunchesHandler.MaxBatchSize + 1)
                .Select(i => ("12", T(10, 0, 0).AddSeconds(i * 3)))
                .ToArray();

            var result = await Ingest(serial, token, tooMany);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Unauthorized, Is.False);
            Assert.That(_context.HrBiometricPunch.Any(), Is.False);
        }

        [Test]
        public async Task RotatingTheToken_KillsTheOldOne_AndTheNewOneWorks()
        {
            var (serial, oldToken) = await RegisteredDevice();
            var deviceId = _context.HrBiometricDevice.Single().HrBiometricDeviceId;

            var rotated = await new RotateHrBiometricDeviceTokenHandler(_context).Handle(
                new RotateHrBiometricDeviceTokenRequestModel { HrBiometricDeviceId = deviceId, LoggedInUserId = _managerA }, CancellationToken.None);

            Assert.That(rotated.Success, Is.True);
            Assert.That((await Ingest(serial, oldToken, ("12", T(10, 9, 30)))).Unauthorized, Is.True);
            Assert.That((await Ingest(serial, rotated.Token, ("12", T(10, 9, 30)))).Success, Is.True);
        }

        // ─── Managing someone else's device ──────────────────────────────────

        [Test]
        public async Task SetActive_ByAManagerOfAnotherHospital_IsDeviceNotFound_AndTheDeviceIsUnchanged()
        {
            await RegisteredDevice();
            var deviceId = _context.HrBiometricDevice.Single().HrBiometricDeviceId;

            var result = await new SetHrBiometricDeviceActiveHandler(_context).Handle(
                new SetHrBiometricDeviceActiveRequestModel { HrBiometricDeviceId = deviceId, IsActive = false, LoggedInUserId = _managerB }, CancellationToken.None);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Is.EqualTo("Device not found."));
            Assert.That(_context.HrBiometricDevice.Single().IsActive, Is.True);
        }

        [Test]
        public async Task RotateToken_ByAManagerOfAnotherHospital_IsDeviceNotFound_AndTheTokenIsUnchanged()
        {
            var (serial, token) = await RegisteredDevice();
            var deviceId = _context.HrBiometricDevice.Single().HrBiometricDeviceId;

            var result = await new RotateHrBiometricDeviceTokenHandler(_context).Handle(
                new RotateHrBiometricDeviceTokenRequestModel { HrBiometricDeviceId = deviceId, LoggedInUserId = _managerB }, CancellationToken.None);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Token, Is.Null, "a stranger must never receive a device token");
            Assert.That((await Ingest(serial, token, ("12", T(10, 9, 30)))).Success, Is.True, "the original token still works");
        }

        // ─── Mapping a PIN to an employee ────────────────────────────────────

        [Test]
        public async Task Map_LinksThePin_AndBuildsAttendanceFromScansThatWereWaiting()
        {
            var (serial, token) = await RegisteredDevice();
            await Ingest(serial, token, ("12", T(10, 9, 30)), ("12", T(10, 17, 30)));
            Assert.That(_context.HrAttendanceLog.Any(), Is.False, "PIN 12 isn't mapped yet");

            var result = await Map(_managerA, _asha.HrEmployeeId, "12");

            Assert.That(result.Success, Is.True);
            Assert.That(result.AttendanceDaysUpdated, Is.EqualTo(1));
            var log = _context.HrAttendanceLog.Single();
            Assert.That(log.HrEmployeeId, Is.EqualTo(_asha.HrEmployeeId));
            Assert.That(log.PunchIn, Is.EqualTo(T(10, 9, 30)));
        }

        [Test]
        public async Task Map_APinAlreadyUsedByAColleague_IsRefused_NamingThem()
        {
            var bikram = NewEmployee(_hospitalA, "EMP-2026-0002", "Bikram");
            _context.HrEmployee.Add(bikram);
            await _context.SaveChangesAsync();
            await Map(_managerA, bikram.HrEmployeeId, "12");

            var result = await Map(_managerA, _asha.HrEmployeeId, "12");

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("Bikram"));
            Assert.That(_context.HrEmployeeDeviceUser.Single().HrEmployeeId, Is.EqualTo(bikram.HrEmployeeId));
        }

        [Test]
        public async Task Map_TheSamePinAtAnotherHospital_IsFine()
        {
            var stranger = NewEmployee(_hospitalB, "EMP-2026-0001", "Chitra");
            _context.HrEmployee.Add(stranger);
            await _context.SaveChangesAsync();
            await Map(_managerB, stranger.HrEmployeeId, "12");

            var result = await Map(_managerA, _asha.HrEmployeeId, "12");

            Assert.That(result.Success, Is.True, "different hospitals legitimately reuse the same PINs");
            Assert.That(_context.HrEmployeeDeviceUser.Count(), Is.EqualTo(2));
        }

        [Test]
        public async Task Map_ChangingAnEmployeesPin_ReplacesTheOldOne()
        {
            await Map(_managerA, _asha.HrEmployeeId, "12");

            await Map(_managerA, _asha.HrEmployeeId, "13");

            Assert.That(_context.HrEmployeeDeviceUser.Single().DeviceUserId, Is.EqualTo("13"));
        }

        [Test]
        public async Task Map_ByAManagerOfAnotherHospital_IsEmployeeNotFound_AndNothingIsLinked()
        {
            var result = await Map(_managerB, _asha.HrEmployeeId, "12");

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Is.EqualTo("Employee not found."));
            Assert.That(_context.HrEmployeeDeviceUser.Any(), Is.False);
        }

        [TestCase("")]
        [TestCase("   ")]
        public async Task Map_ABlankPin_IsRefused(string pin)
        {
            var result = await Map(_managerA, _asha.HrEmployeeId, pin);

            Assert.That(result.Success, Is.False);
        }

        [Test]
        public async Task Unmap_RemovesTheLink_ButKeepsAttendanceAlreadyBuilt()
        {
            var (serial, token) = await RegisteredDevice();
            await Map(_managerA, _asha.HrEmployeeId, "12");
            await Ingest(serial, token, ("12", T(10, 9, 30)), ("12", T(10, 17, 30)));

            var result = await new UnmapEmployeeDeviceUserHandler(_context).Handle(
                new UnmapEmployeeDeviceUserRequestModel { HrEmployeeId = _asha.HrEmployeeId, LoggedInUserId = _managerA }, CancellationToken.None);

            Assert.That(result.Success, Is.True);
            Assert.That(_context.HrEmployeeDeviceUser.Any(), Is.False);
            Assert.That(_context.HrAttendanceLog.Count(), Is.EqualTo(1), "history stays");
        }

        [Test]
        public async Task Unmap_ByAManagerOfAnotherHospital_IsEmployeeNotFound_AndTheLinkRemains()
        {
            await Map(_managerA, _asha.HrEmployeeId, "12");

            var result = await new UnmapEmployeeDeviceUserHandler(_context).Handle(
                new UnmapEmployeeDeviceUserRequestModel { HrEmployeeId = _asha.HrEmployeeId, LoggedInUserId = _managerB }, CancellationToken.None);

            Assert.That(result.Success, Is.False);
            Assert.That(_context.HrEmployeeDeviceUser.Count(), Is.EqualTo(1));
        }

        // ─── Queries ─────────────────────────────────────────────────────────

        [Test]
        public async Task GetDevices_ListsOnlyThisHospitalsDevices_AndFlagsOnlineByRecency()
        {
            await Register(_managerA, _hospitalA, "Main gate", "AAAA11111");
            await Register(_managerA, _hospitalA, "Ward 2", "BBBB22222");
            await Register(_managerB, _hospitalB, "Elsewhere", "CCCC33333");
            _context.HrBiometricDevice.Single(d => d.SerialNumber == "AAAA11111").LastSeenAt = DateTime.UtcNow.AddMinutes(-2);
            _context.HrBiometricDevice.Single(d => d.SerialNumber == "BBBB22222").LastSeenAt = DateTime.UtcNow.AddMinutes(-45);
            await _context.SaveChangesAsync();

            var result = await new GetHrBiometricDevicesHandler(_context).Handle(new GetHrBiometricDevicesRequestModel { HospitalId = _hospitalA }, CancellationToken.None);

            Assert.That(result.Devices.Select(d => d.SerialNumber), Is.EquivalentTo(new[] { "AAAA11111", "BBBB22222" }));
            Assert.That(result.Devices.Single(d => d.SerialNumber == "AAAA11111").IsOnline, Is.True);
            Assert.That(result.Devices.Single(d => d.SerialNumber == "BBBB22222").IsOnline, Is.False);
        }

        [Test]
        public async Task GetDevices_NeverExposesTheTokenHash()
        {
            await Register(_managerA, _hospitalA, "Main gate", "AAAA11111");

            var json = System.Text.Json.JsonSerializer.Serialize(
                await new GetHrBiometricDevicesHandler(_context).Handle(new GetHrBiometricDevicesRequestModel { HospitalId = _hospitalA }, CancellationToken.None));

            Assert.That(json, Does.Not.Contain(_context.HrBiometricDevice.Single().TokenHash));
            Assert.That(json.ToLowerInvariant(), Does.Not.Contain("tokenhash"));
        }

        [Test]
        public async Task GetUnmappedDeviceUsers_GroupsScansByPin_AndSkipsMappedOnes()
        {
            var (serial, token) = await RegisteredDevice();
            await Map(_managerA, _asha.HrEmployeeId, "12");
            await Ingest(serial, token, ("12", T(10, 9, 0)), ("77", T(10, 9, 5)), ("77", T(11, 9, 0)), ("88", T(10, 10, 0)));

            var result = await new GetUnmappedDeviceUsersHandler(_context).Handle(new GetUnmappedDeviceUsersRequestModel { HospitalId = _hospitalA }, CancellationToken.None);

            Assert.That(result.Users.Select(u => u.DeviceUserId), Is.EquivalentTo(new[] { "77", "88" }));
            var seventySeven = result.Users.Single(u => u.DeviceUserId == "77");
            Assert.That(seventySeven.ScanCount, Is.EqualTo(2));
            Assert.That(seventySeven.LastSeen, Is.EqualTo(T(11, 9, 0)));
        }

        [Test]
        public async Task GetUnmappedDeviceUsers_DoesNotShowAnotherHospitalsScans()
        {
            var (serial, token) = await RegisteredDevice();
            await Ingest(serial, token, ("77", T(10, 9, 5)));

            var result = await new GetUnmappedDeviceUsersHandler(_context).Handle(new GetUnmappedDeviceUsersRequestModel { HospitalId = _hospitalB }, CancellationToken.None);

            Assert.That(result.Users, Is.Empty);
        }

        [Test]
        public async Task GetEmployeeDeviceUsers_ListsLinksWithNames_ForThisHospitalOnly()
        {
            var stranger = NewEmployee(_hospitalB, "EMP-2026-0001", "Chitra");
            _context.HrEmployee.Add(stranger);
            await _context.SaveChangesAsync();
            await Map(_managerA, _asha.HrEmployeeId, "12");
            await Map(_managerB, stranger.HrEmployeeId, "12");

            var result = await new GetEmployeeDeviceUsersHandler(_context).Handle(new GetEmployeeDeviceUsersRequestModel { HospitalId = _hospitalA }, CancellationToken.None);

            var link = result.Links.Single();
            Assert.That(link.EmployeeName, Is.EqualTo("Asha Tester"));
            Assert.That(link.DeviceUserId, Is.EqualTo("12"));
        }

        // ─── helpers ─────────────────────────────────────────────────────────

        private Task<RegisterHrBiometricDeviceResponseModel> Register(Guid caller, Guid hospital, string name, string serial) =>
            new RegisterHrBiometricDeviceHandler(_context).Handle(
                new RegisterHrBiometricDeviceRequestModel { HospitalId = hospital, Name = name, SerialNumber = serial, Model = "K40 Pro", LoggedInUserId = caller },
                CancellationToken.None);

        /// <summary>Registers a device at hospital A and returns its serial and (one-time) token.</summary>
        private async Task<(string Serial, string Token)> RegisteredDevice()
        {
            var registered = await Register(_managerA, _hospitalA, "Main gate", "CJDE192360123");
            return (registered.Device!.SerialNumber, registered.Token!);
        }

        private Task<IngestBiometricPunchesResponseModel> Ingest(string? serial, string? token, params (string Pin, DateTime Time)[] scans) =>
            new IngestBiometricPunchesHandler(_context, _ingestion).Handle(
                new IngestBiometricPunchesRequestModel
                {
                    DeviceSerial = serial,
                    DeviceToken = token,
                    RemoteIp = "203.0.113.7",
                    Punches = scans.Select(s => new BiometricPunchDto { UserId = s.Pin, Time = s.Time }).ToList(),
                },
                CancellationToken.None);

        private Task<MapEmployeeDeviceUserResponseModel> Map(Guid caller, Guid employeeId, string pin) =>
            new MapEmployeeDeviceUserHandler(_context, _ingestion).Handle(
                new MapEmployeeDeviceUserRequestModel { HrEmployeeId = employeeId, DeviceUserId = pin, LoggedInUserId = caller },
                CancellationToken.None);

        private static HrEmployee NewEmployee(Guid hospitalId, string code, string firstName) => new()
        {
            HrEmployeeId = Guid.NewGuid(),
            HospitalId = hospitalId,
            EmployeeCode = code,
            FirstName = firstName,
            LastName = "Tester",
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
    }
}
