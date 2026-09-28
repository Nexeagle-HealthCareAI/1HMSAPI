using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class GetAttendanceExceptionsHandlerTests
    {
        private AppDbContext _context = null!;
        private GetAttendanceExceptionsHandler _handler = null!;
        private Guid _hospitalId;
        private HrEmployee _employee = null!;

        private static readonly DateOnly Day = new(2026, 8, 10);

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetAttendanceExceptionsHandler(_context);
            _hospitalId = Guid.NewGuid();
            // HrEmployee.DepartmentId is a required (non-nullable) FK, so the handler's
            // .Include(e => e.Department) is an inner join in EF's InMemory provider -- an
            // employee with no matching Department row silently drops out of every result,
            // which would make every "expect empty" test below pass for the wrong reason.
            var departmentId = Guid.NewGuid();
            _context.Departments.Add(new Department { DepartmentID = departmentId, Name = "Nursing" });
            _employee = new HrEmployee
            {
                HrEmployeeId = Guid.NewGuid(), HospitalId = _hospitalId, EmployeeCode = "EMP-2026-0001",
                FirstName = "Asha", LastName = "Tester", Gender = "Female", DateOfBirth = new DateOnly(1990, 1, 1),
                ContactNumber = "+91-9800000000", EmploymentType = "FULL_TIME_SALARIED", DepartmentId = departmentId,
                Designation = "Staff Nurse", DateOfJoining = new DateOnly(2020, 1, 1), PanNumber = "ABCDE1234F",
                PayrollTrack = "TRACK_A_SALARIED", IsActive = true, Status = "ACTIVE",
            };
            _context.HrEmployee.Add(_employee);
            _context.SaveChanges();
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        private async Task<Application.ResponseModels.QueryResponseModels.AttendanceExceptionDto> RunAndGetSingle()
        {
            var result = await _handler.Handle(new GetAttendanceExceptionsRequestModel
            {
                HospitalId = _hospitalId,
                StartDate = new DateTime(2026, 8, 1),
                EndDate = new DateTime(2026, 8, 31),
            }, CancellationToken.None);
            return result.Exceptions.Single();
        }

        [Test]
        public async Task AbsentDay_IsSurfacedAsAnException()
        {
            _context.HrAttendanceLog.Add(new HrAttendanceLog { HrEmployeeId = _employee.HrEmployeeId, AttendanceDate = Day, Status = "ABSENT" });
            await _context.SaveChangesAsync();

            var exception = await RunAndGetSingle();

            Assert.That(exception.ExceptionType, Is.EqualTo("ABSENT"));
            Assert.That(exception.EmployeeName, Is.EqualTo("Asha Tester"));
        }

        [Test]
        public async Task OnLeaveDay_IsNotSurfacedAsAnException()
        {
            _context.HrAttendanceLog.Add(new HrAttendanceLog { HrEmployeeId = _employee.HrEmployeeId, AttendanceDate = Day, Status = "ON_LEAVE" });
            await _context.SaveChangesAsync();

            var result = await _handler.Handle(new GetAttendanceExceptionsRequestModel { HospitalId = _hospitalId, StartDate = new DateTime(2026, 8, 1), EndDate = new DateTime(2026, 8, 31) }, CancellationToken.None);

            Assert.That(result.Exceptions, Is.Empty, "approved leave is expected, not an anomaly to chase");
        }

        [Test]
        public async Task LateDay_StillFlagsAsLate_NotAbsent()
        {
            _context.HrAttendanceLog.Add(new HrAttendanceLog { HrEmployeeId = _employee.HrEmployeeId, AttendanceDate = Day, Status = "LATE", PunchIn = new DateTime(2026, 8, 10, 9, 45, 0) });
            await _context.SaveChangesAsync();

            var exception = await RunAndGetSingle();

            Assert.That(exception.ExceptionType, Is.EqualTo("LATE"));
        }

        [Test]
        public async Task PresentDay_IsNotAnException()
        {
            _context.HrAttendanceLog.Add(new HrAttendanceLog
            {
                HrEmployeeId = _employee.HrEmployeeId, AttendanceDate = Day, Status = "PRESENT",
                PunchIn = new DateTime(2026, 8, 10, 9, 0, 0), PunchOut = new DateTime(2026, 8, 10, 17, 0, 0),
            });
            await _context.SaveChangesAsync();

            var result = await _handler.Handle(new GetAttendanceExceptionsRequestModel { HospitalId = _hospitalId, StartDate = new DateTime(2026, 8, 1), EndDate = new DateTime(2026, 8, 31) }, CancellationToken.None);

            Assert.That(result.Exceptions, Is.Empty);
        }

        [Test]
        public async Task AnotherHospitalsAbsentDay_IsNotReturned()
        {
            var otherDepartmentId = Guid.NewGuid();
            _context.Departments.Add(new Department { DepartmentID = otherDepartmentId, Name = "Wards" });
            var otherHospitalEmployee = new HrEmployee
            {
                HrEmployeeId = Guid.NewGuid(), HospitalId = Guid.NewGuid(), EmployeeCode = "EMP-2026-0001",
                FirstName = "Bikram", LastName = "Other", Gender = "Male", DateOfBirth = new DateOnly(1990, 1, 1),
                ContactNumber = "+91-9800000001", EmploymentType = "FULL_TIME_SALARIED", DepartmentId = otherDepartmentId,
                Designation = "Ward Boy", DateOfJoining = new DateOnly(2020, 1, 1), PanNumber = "ABCDE1235F",
                PayrollTrack = "TRACK_A_SALARIED", IsActive = true, Status = "ACTIVE",
            };
            _context.HrEmployee.Add(otherHospitalEmployee);
            _context.HrAttendanceLog.Add(new HrAttendanceLog { HrEmployeeId = otherHospitalEmployee.HrEmployeeId, AttendanceDate = Day, Status = "ABSENT" });
            await _context.SaveChangesAsync();

            var result = await _handler.Handle(new GetAttendanceExceptionsRequestModel { HospitalId = _hospitalId, StartDate = new DateTime(2026, 8, 1), EndDate = new DateTime(2026, 8, 31) }, CancellationToken.None);

            Assert.That(result.Exceptions, Is.Empty, "the other hospital's ABSENT employee proves the row would have surfaced if hospital scoping didn't work");
        }
    }
}
