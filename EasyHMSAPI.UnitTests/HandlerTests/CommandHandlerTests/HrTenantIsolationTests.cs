using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    /// <summary>
    /// HR endpoints that take a record ID (payroll run, leave request, employee) carry no hospitalId, so
    /// HospitalAccessFilter never checks them and [RequiresPermission] is not hospital-scoped. These tests
    /// pin that a manager at one hospital cannot read or change another hospital's HR data, and that the
    /// biometric punch is resolved inside the hospital it names (employee codes repeat across hospitals).
    /// </summary>
    [TestFixture]
    public class HrTenantIsolationTests
    {
        private AppDbContext _context = null!;
        private Guid _hospitalA;
        private Guid _hospitalB;
        // Several HR handlers join each employee to its department (an INNER JOIN, since DepartmentId is
        // non-nullable), so the department row must exist or those queries silently return nothing --
        // which would also make every "sees nothing" test below pass for the wrong reason.
        private Guid _departmentId;
        private HrEmployee _empA = null!;
        private HrEmployee _empB = null!;
        private HrPayrollRun _runA = null!;
        private Guid _managerA;      // payroll + leave manager at hospital A
        private Guid _managerB;      // payroll + leave manager at hospital B only
        private Guid _plainMemberA;  // belongs to hospital A, no HR permissions

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _hospitalA = Guid.NewGuid();
            _hospitalB = Guid.NewGuid();

            AddHospital(_hospitalA, "Hospital A");
            AddHospital(_hospitalB, "Hospital B");
            _departmentId = Guid.NewGuid();
            _context.Departments.Add(new Department { DepartmentID = _departmentId, Name = "Nursing" });

            // Same EmployeeCode at both hospitals -- exactly what the EMP-YYYY-NNNN generator produces.
            _empA = NewEmployee(_hospitalA, "EMP-2026-0001", "Asha");
            _empB = NewEmployee(_hospitalB, "EMP-2026-0001", "Bikram");
            _context.HrEmployee.AddRange(_empA, _empB);

            _runA = new HrPayrollRun { HrPayrollRunId = Guid.NewGuid(), HospitalId = _hospitalA, Month = 8, Year = 2026, Status = "DRAFT" };
            _context.HrPayrollRun.Add(_runA);
            _context.HrPayslip.Add(NewPayslip(_runA.HrPayrollRunId, _empA, 41250m));
            _context.SaveChanges();

            _managerA = HrAuthSeed.SeedMember(_context, _hospitalA, "hr.manage_payroll", "hr.manage_leaves");
            _managerB = HrAuthSeed.SeedMember(_context, _hospitalB, "hr.manage_payroll", "hr.manage_leaves");
            _plainMemberA = HrAuthSeed.SeedMember(_context, _hospitalA);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        // ─── Bank-file export ────────────────────────────────────────────────

        [Test]
        public async Task ExportBankFile_ManagerAtTheRunsHospital_ReturnsTheFile()
        {
            var result = await new ExportBankFileHandler(_context).Handle(
                new ExportBankFileRequestModel { HrPayrollRunId = _runA.HrPayrollRunId, BankFormat = "GENERIC", LoggedInUserId = _managerA }, CancellationToken.None);

            Assert.That(result.Success, Is.True);
            Assert.That(Encoding.UTF8.GetString(result.FileBytes!), Does.Contain("Asha"));
        }

        [Test]
        public async Task ExportBankFile_ManagerOfAnotherHospital_GetsNotFoundAndRunIsUntouched()
        {
            var result = await new ExportBankFileHandler(_context).Handle(
                new ExportBankFileRequestModel { HrPayrollRunId = _runA.HrPayrollRunId, LoggedInUserId = _managerB }, CancellationToken.None);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Is.EqualTo("Payroll run not found."));
            Assert.That(result.FileBytes, Is.Null, "no bank details may leave the hospital");
            Assert.That(_context.HrPayrollRun.Single().Status, Is.EqualTo("DRAFT"), "a refused export must not flip the run to DISBURSED");
        }

        [Test]
        public async Task ExportBankFile_MemberWithoutPayrollPermission_IsDenied()
        {
            var result = await new ExportBankFileHandler(_context).Handle(
                new ExportBankFileRequestModel { HrPayrollRunId = _runA.HrPayrollRunId, LoggedInUserId = _plainMemberA }, CancellationToken.None);

            Assert.That(result.Success, Is.False);
        }

        [Test]
        public async Task ExportBankFile_UnidentifiedCaller_IsDenied()
        {
            var result = await new ExportBankFileHandler(_context).Handle(
                new ExportBankFileRequestModel { HrPayrollRunId = _runA.HrPayrollRunId, LoggedInUserId = Guid.Empty }, CancellationToken.None);

            Assert.That(result.Success, Is.False);
        }

        // ─── Payslip dispatch (WhatsApp) ─────────────────────────────────────

        [Test]
        public async Task DispatchPayslips_ManagerAtTheRunsHospital_SendsToItsEmployees()
        {
            var whatsApp = NewWhatsApp();

            var result = await new DispatchPayslipsHandler(_context, whatsApp.Object).Handle(
                new DispatchPayslipsRequestModel { HrPayrollRunId = _runA.HrPayrollRunId, LoggedInUserId = _managerA }, CancellationToken.None);

            Assert.That(result.Success, Is.True);
            Assert.That(result.DispatchedCount, Is.EqualTo(1));
        }

        [Test]
        public async Task DispatchPayslips_ManagerOfAnotherHospital_MessagesNobodyAndLeavesRunAlone()
        {
            var whatsApp = NewWhatsApp();

            var result = await new DispatchPayslipsHandler(_context, whatsApp.Object).Handle(
                new DispatchPayslipsRequestModel { HrPayrollRunId = _runA.HrPayrollRunId, LoggedInUserId = _managerB }, CancellationToken.None);

            Assert.That(result.Success, Is.False);
            whatsApp.Verify(w => w.SendPayslipNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>()), Times.Never);
            Assert.That(_context.HrPayrollRun.Single().Status, Is.EqualTo("DRAFT"));
        }

        // ─── Payslip listing ─────────────────────────────────────────────────

        [Test]
        public async Task GetPayslipsByRun_ManagerAtTheRunsHospital_SeesAllPayslips()
        {
            var result = await new GetPayslipsByRunHandler(_context).Handle(
                new GetPayslipsByRunRequestModel { HrPayrollRunId = _runA.HrPayrollRunId, LoggedInUserId = _managerA }, CancellationToken.None);

            Assert.That(result.Payslips, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task GetPayslipsByRun_ManagerOfAnotherHospital_SeesNothing()
        {
            var result = await new GetPayslipsByRunHandler(_context).Handle(
                new GetPayslipsByRunRequestModel { HrPayrollRunId = _runA.HrPayrollRunId, LoggedInUserId = _managerB }, CancellationToken.None);

            Assert.That(result.Payslips, Is.Empty, "PAN, bank account and salary of another hospital's staff must stay hidden");
        }

        [Test]
        public async Task GetPayslipsByRun_OrdinaryEmployee_SeesOnlyTheirOwnPayslip()
        {
            var otherEmployee = NewEmployee(_hospitalA, "EMP-2026-0002", "Chitra");
            _empA.UserId = _plainMemberA;
            _context.HrEmployee.Add(otherEmployee);
            _context.HrPayslip.Add(NewPayslip(_runA.HrPayrollRunId, otherEmployee, 30000m));
            await _context.SaveChangesAsync();

            var result = await new GetPayslipsByRunHandler(_context).Handle(
                new GetPayslipsByRunRequestModel { HrPayrollRunId = _runA.HrPayrollRunId, LoggedInUserId = _plainMemberA }, CancellationToken.None);

            Assert.That(result.Payslips, Has.Count.EqualTo(1));
            Assert.That(result.Payslips[0].EmployeeName, Does.StartWith("Asha"));
        }

        // ─── Leave lists / balances ──────────────────────────────────────────

        [Test]
        public async Task GetLeaveRequests_ManagerAtTheHospital_SeesItsRequests()
        {
            SeedPendingLeave(_empA);

            var result = await new GetHrLeaveRequestsHandler(_context).Handle(
                new GetHrLeaveRequestsRequestModel { HospitalId = _hospitalA, LoggedInUserId = _managerA }, CancellationToken.None);

            Assert.That(result.LeaveRequests, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task GetLeaveRequests_ManagerOmittingHospitalId_DoesNotSeeEveryHospital()
        {
            SeedPendingLeave(_empA);
            SeedPendingLeave(_empB);

            var result = await new GetHrLeaveRequestsHandler(_context).Handle(
                new GetHrLeaveRequestsRequestModel { HospitalId = null, LoggedInUserId = _managerA }, CancellationToken.None);

            Assert.That(result.LeaveRequests, Is.Empty, "hospitalId is optional on the endpoint; leaving it out must not widen the view");
        }

        [Test]
        public async Task GetLeaveRequests_ManagerAskingForAnotherHospital_GetsNothing()
        {
            SeedPendingLeave(_empA);

            var result = await new GetHrLeaveRequestsHandler(_context).Handle(
                new GetHrLeaveRequestsRequestModel { HospitalId = _hospitalA, LoggedInUserId = _managerB }, CancellationToken.None);

            Assert.That(result.LeaveRequests, Is.Empty);
        }

        [Test]
        public async Task GetLeaveBalance_ManagerAtTheEmployeesHospital_SeesTheBalance()
        {
            _context.HrLeaveBalance.Add(new HrLeaveBalance { HrEmployeeId = _empA.HrEmployeeId, Year = 2026 });
            await _context.SaveChangesAsync();

            var result = await new GetHrLeaveBalanceHandler(_context).Handle(
                new GetHrLeaveBalanceRequestModel { EmployeeId = _empA.HrEmployeeId, Year = 2026, LoggedInUserId = _managerA }, CancellationToken.None);

            Assert.That(result.LeaveBalance, Is.Not.Null);
        }

        [Test]
        public async Task GetLeaveBalance_ManagerOfAnotherHospital_GetsNothing()
        {
            _context.HrLeaveBalance.Add(new HrLeaveBalance { HrEmployeeId = _empA.HrEmployeeId, Year = 2026 });
            await _context.SaveChangesAsync();

            var result = await new GetHrLeaveBalanceHandler(_context).Handle(
                new GetHrLeaveBalanceRequestModel { EmployeeId = _empA.HrEmployeeId, Year = 2026, LoggedInUserId = _managerB }, CancellationToken.None);

            Assert.That(result.LeaveBalance, Is.Null);
        }

        // ─── Biometric punch ─────────────────────────────────────────────────

        [Test]
        public async Task ProcessBiometricPunch_SameCodeAtTwoHospitals_AttachesOnlyToTheHospitalNamed()
        {
            var result = await new ProcessBiometricPunchHandler(_context).Handle(new ProcessBiometricPunchRequestModel
            {
                HospitalId = _hospitalB,
                EmployeeCode = "EMP-2026-0001",
                DeviceId = "K40-B-01",
                PunchType = "IN",
                PunchTime = new DateTime(2026, 8, 10, 9, 5, 0),
            }, CancellationToken.None);

            Assert.That(result.Success, Is.True);
            var log = _context.HrAttendanceLog.Single();
            Assert.That(log.HrEmployeeId, Is.EqualTo(_empB.HrEmployeeId), "must be hospital B's employee, not hospital A's with the same code");
        }

        [Test]
        public async Task ProcessBiometricPunch_CodeThatOnlyExistsAtAnotherHospital_IsRejectedAndNothingRecorded()
        {
            var thirdHospital = Guid.NewGuid();

            var result = await new ProcessBiometricPunchHandler(_context).Handle(new ProcessBiometricPunchRequestModel
            {
                HospitalId = thirdHospital,
                EmployeeCode = "EMP-2026-0001",
                DeviceId = "K40-C-01",
                PunchType = "IN",
                PunchTime = new DateTime(2026, 8, 10, 9, 5, 0),
            }, CancellationToken.None);

            Assert.That(result.Success, Is.False);
            Assert.That(_context.HrAttendanceLog.Any(), Is.False);
        }

        // ─── helpers ─────────────────────────────────────────────────────────

        private void AddHospital(Guid id, string name)
        {
            _context.Hospitals.Add(new Hospital
            {
                HospitalID = id, Name = name, Email = "e@m.com", Type = "General", RegistrationNumber = "REG-" + id.ToString()[..6],
                Contact = "1234567890", Location = "Test Location", City = "Test City", State = "Test State",
                Country = "Test Country", Pincode = "123456", CreatedByUserID = Guid.NewGuid(),
            });
        }

        private HrEmployee NewEmployee(Guid hospitalId, string code, string firstName) => new()
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
            DepartmentId = _departmentId,
            Designation = "Staff Nurse",
            DateOfJoining = new DateOnly(2020, 1, 1),
            PanNumber = "ABCDE1234F",
            PayrollTrack = "TRACK_A_SALARIED",
            IsActive = true,
            Status = "ACTIVE",
        };

        private static HrPayslip NewPayslip(Guid runId, HrEmployee employee, decimal net) => new()
        {
            HrPayslipId = Guid.NewGuid(),
            HrPayrollRunId = runId,
            HrEmployeeId = employee.HrEmployeeId,
            PayslipNumber = "PAY-2026-08-" + employee.EmployeeCode,
            PayrollTrack = "TRACK_A_SALARIED",
            NetSalary = net,
        };

        private void SeedPendingLeave(HrEmployee employee)
        {
            _context.HrLeaveRequest.Add(new HrLeaveRequest
            {
                HrLeaveRequestId = Guid.NewGuid(),
                HrEmployeeId = employee.HrEmployeeId,
                LeaveType = "CASUAL",
                StartDate = new DateOnly(2026, 8, 10),
                EndDate = new DateOnly(2026, 8, 10),
                TotalDays = 1m,
                Reason = "Personal",
                Status = "PENDING",
            });
            _context.SaveChanges();
        }

        private static Mock<IWhatsAppMessagingService> NewWhatsApp()
        {
            var mock = new Mock<IWhatsAppMessagingService>();
            mock.Setup(w => w.SendPayslipNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>()))
                .ReturnsAsync(true);
            return mock;
        }
    }
}
