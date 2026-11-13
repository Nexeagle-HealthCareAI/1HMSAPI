using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class SetAttendanceOverrideHandlerTests
    {
        private AppDbContext _context = null!;
        private SetAttendanceOverrideHandler _handler = null!;
        private Guid _hospitalId;
        private HrEmployee _employee = null!;
        private Guid _manager;

        private static readonly DateOnly Day = new(2026, 8, 10);

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new SetAttendanceOverrideHandler(_context);
            _hospitalId = Guid.NewGuid();
            _employee = new HrEmployee
            {
                HrEmployeeId = Guid.NewGuid(), HospitalId = _hospitalId, EmployeeCode = "EMP-2026-0001",
                FirstName = "Asha", LastName = "Tester", Gender = "Female", DateOfBirth = new DateOnly(1990, 1, 1),
                ContactNumber = "+91-9800000000", EmploymentType = "FULL_TIME_SALARIED", DepartmentId = Guid.NewGuid(),
                Designation = "Staff Nurse", DateOfJoining = new DateOnly(2020, 1, 1), PanNumber = "ABCDE1234F",
                PayrollTrack = "TRACK_A_SALARIED", IsActive = true, Status = "ACTIVE",
            };
            _context.HrEmployee.Add(_employee);
            _context.SaveChanges();
            _manager = HrAuthSeed.SeedMember(_context, _hospitalId, "hr.manage_employees");
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        private SetAttendanceOverrideRequestModel Request(string status = "PRESENT", string reason = "Forgot to scan", Guid? caller = null) => new()
        {
            HrEmployeeId = _employee.HrEmployeeId,
            AttendanceDate = Day,
            Status = status,
            Reason = reason,
            LoggedInUserId = caller ?? _manager,
        };

        [Test]
        public async Task CreatesANewRecordForADayWithNoScans_AndStampsTheAudit()
        {
            var request = Request();
            request.PunchIn = new DateTime(2026, 8, 10, 9, 0, 0);
            request.PunchOut = new DateTime(2026, 8, 10, 17, 0, 0);
            request.Notes = "Manager confirmed she was on-site";

            var result = await _handler.Handle(request, CancellationToken.None);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Message, Does.Contain("created"));
            var log = _context.HrAttendanceLog.Single();
            Assert.That(log.Status, Is.EqualTo("PRESENT"));
            Assert.That(log.PunchSource, Is.EqualTo("MANUAL_OVERRIDE"));
            Assert.That(log.TotalHoursWorked, Is.EqualTo(8.00m));
            Assert.That(log.Notes, Is.EqualTo("Manager confirmed she was on-site"));
            Assert.That(log.OverriddenByUserId, Is.EqualTo(_manager));
            Assert.That(log.OverriddenAt, Is.Not.Null);
            Assert.That(log.OverrideReason, Is.EqualTo("Forgot to scan"));
        }

        [Test]
        public async Task CreatingWithNoPunchTimes_LeavesHoursNull()
        {
            var result = await _handler.Handle(Request("ABSENT"), CancellationToken.None);

            Assert.That(result.Success, Is.True);
            var log = _context.HrAttendanceLog.Single();
            Assert.That(log.PunchIn, Is.Null);
            Assert.That(log.TotalHoursWorked, Is.Null);
        }

        [Test]
        public async Task CorrectingAnExistingRecord_OnlyChangesWhatWasSent()
        {
            _context.HrAttendanceLog.Add(new HrAttendanceLog
            {
                HrEmployeeId = _employee.HrEmployeeId, AttendanceDate = Day,
                PunchIn = new DateTime(2026, 8, 10, 9, 40, 0), Status = "LATE", PunchSource = "BIOMETRIC",
                OvertimeHours = 0.5m,
            });
            await _context.SaveChangesAsync();

            var request = Request("PRESENT", "Device clock was wrong; she badged in on time per the gate log");
            request.PunchIn = new DateTime(2026, 8, 10, 9, 5, 0);
            // PunchOut intentionally omitted.

            var result = await _handler.Handle(request, CancellationToken.None);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Message, Does.Contain("updated"));
            var log = _context.HrAttendanceLog.Single();
            Assert.That(log.Status, Is.EqualTo("PRESENT"));
            Assert.That(log.PunchIn, Is.EqualTo(new DateTime(2026, 8, 10, 9, 5, 0)));
            Assert.That(log.PunchOut, Is.Null, "was never set and wasn't sent this time either");
            Assert.That(log.OvertimeHours, Is.EqualTo(0.5m), "not part of this override's inputs, so left untouched");
        }

        [Test]
        public async Task NotesSentAsEmptyString_ClearsAnExistingNote()
        {
            _context.HrAttendanceLog.Add(new HrAttendanceLog { HrEmployeeId = _employee.HrEmployeeId, AttendanceDate = Day, Status = "PRESENT", Notes = "UNSCHEDULED" });
            await _context.SaveChangesAsync();

            var request = Request();
            request.Notes = "";

            await _handler.Handle(request, CancellationToken.None);

            Assert.That(_context.HrAttendanceLog.Single().Notes, Is.Null);
        }

        [TestCase("present")]
        [TestCase("Late")]
        public async Task StatusIsCaseInsensitive(string status)
        {
            var result = await _handler.Handle(Request(status), CancellationToken.None);

            Assert.That(result.Success, Is.True);
            Assert.That(_context.HrAttendanceLog.Single().Status, Is.EqualTo(status.ToUpperInvariant()));
        }

        [TestCase("")]
        [TestCase("ON_HOLIDAY")]
        [TestCase("weekly_off")]
        public async Task UnrecognisedStatus_IsRefusedAndNothingIsWritten(string status)
        {
            var result = await _handler.Handle(Request(status), CancellationToken.None);

            Assert.That(result.Success, Is.False);
            Assert.That(_context.HrAttendanceLog.Any(), Is.False);
        }

        [Test]
        public async Task MissingReason_IsRefused()
        {
            var result = await _handler.Handle(Request(reason: ""), CancellationToken.None);

            Assert.That(result.Success, Is.False);
            Assert.That(_context.HrAttendanceLog.Any(), Is.False);
        }

        [Test]
        public async Task PunchOutBeforePunchIn_IsRefused()
        {
            var request = Request();
            request.PunchIn = new DateTime(2026, 8, 10, 17, 0, 0);
            request.PunchOut = new DateTime(2026, 8, 10, 9, 0, 0);

            var result = await _handler.Handle(request, CancellationToken.None);

            Assert.That(result.Success, Is.False);
            Assert.That(_context.HrAttendanceLog.Any(), Is.False);
        }

        [Test]
        public async Task ManagerOfAnotherHospital_IsDenied_AndNothingIsWritten()
        {
            var otherManager = HrAuthSeed.SeedMember(_context, Guid.NewGuid(), "hr.manage_employees");

            var result = await _handler.Handle(Request(caller: otherManager), CancellationToken.None);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Is.EqualTo("Employee not found."));
            Assert.That(_context.HrAttendanceLog.Any(), Is.False);
        }

        [Test]
        public async Task MemberWithoutThePermission_IsDenied()
        {
            var plainMember = HrAuthSeed.SeedMember(_context, _hospitalId);

            var result = await _handler.Handle(Request(caller: plainMember), CancellationToken.None);

            Assert.That(result.Success, Is.False);
            Assert.That(_context.HrAttendanceLog.Any(), Is.False);
        }
    }
}
