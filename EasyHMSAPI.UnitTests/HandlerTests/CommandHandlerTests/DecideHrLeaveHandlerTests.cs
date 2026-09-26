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
    public class DecideHrLeaveHandlerTests
    {
        private AppDbContext _context = null!;
        private DecideHrLeaveHandler _handler = null!;
        private HrEmployee _employee = null!;
        private Guid _approverId;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new DecideHrLeaveHandler(_context);

            _employee = new HrEmployee
            {
                HrEmployeeId = Guid.NewGuid(),
                HospitalId = Guid.NewGuid(),
                EmployeeCode = "EMP-2026-0001",
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
            _context.HrEmployee.Add(_employee);
            _context.SaveChanges();

            // The approver must be a leave manager AT THE EMPLOYEE'S hospital, not just anyone.
            _approverId = HrAuthSeed.SeedMember(_context, _employee.HospitalId, "hr.manage_leaves");
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        private HrLeaveRequest SeedLeaveRequest(string leaveType, decimal totalDays)
        {
            var leave = new HrLeaveRequest
            {
                HrLeaveRequestId = Guid.NewGuid(),
                HrEmployeeId = _employee.HrEmployeeId,
                LeaveType = leaveType,
                StartDate = new DateOnly(2026, 8, 10),
                EndDate = new DateOnly(2026, 8, 10 + (int)totalDays - 1),
                TotalDays = totalDays,
                Reason = "Personal",
                Status = "PENDING",
            };
            _context.HrLeaveRequest.Add(leave);
            _context.SaveChanges();
            return leave;
        }

        [Test]
        public async Task Handle_ApprovedCasualLeave_DeductsBalanceAndIncrementsUsed()
        {
            _context.HrLeaveBalance.Add(new HrLeaveBalance { HrEmployeeId = _employee.HrEmployeeId, Year = 2026 });
            await _context.SaveChangesAsync();
            var leave = SeedLeaveRequest("CASUAL", 3m);
            var approverId = _approverId;

            var response = await _handler.Handle(new DecideHrLeaveRequestModel
            {
                LeaveId = leave.HrLeaveRequestId,
                Status = "APPROVED",
                ApprovedByUserId = approverId,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            var balance = _context.HrLeaveBalance.Single(b => b.HrEmployeeId == _employee.HrEmployeeId && b.Year == 2026);
            Assert.That(balance.CasualLeaveBalance, Is.EqualTo(9m), "12 default - 3 days taken");
            Assert.That(balance.CasualLeaveUsed, Is.EqualTo(3m));

            var saved = _context.HrLeaveRequest.Single(l => l.HrLeaveRequestId == leave.HrLeaveRequestId);
            Assert.That(saved.Status, Is.EqualTo("APPROVED"));
            Assert.That(saved.ApprovedByUserId, Is.EqualTo(approverId));
            Assert.That(saved.ApprovedAt, Is.Not.Null);
        }

        [Test]
        public async Task Handle_ApprovedLeave_NoExistingBalanceRow_CreatesOneWithDefaultsThenDeducts()
        {
            var leave = SeedLeaveRequest("SICK", 2m);

            await _handler.Handle(new DecideHrLeaveRequestModel
            {
                LeaveId = leave.HrLeaveRequestId,
                Status = "APPROVED",
                ApprovedByUserId = _approverId,
            }, CancellationToken.None);

            var balance = _context.HrLeaveBalance.Single(b => b.HrEmployeeId == _employee.HrEmployeeId && b.Year == 2026);
            Assert.That(balance.SickLeaveBalance, Is.EqualTo(10m), "default 12 - 2 days taken");
            Assert.That(balance.SickLeaveUsed, Is.EqualTo(2m));
        }

        [Test]
        public async Task Handle_ApprovedCompOffLeave_DeductsCompOffBalanceOnly()
        {
            _context.HrLeaveBalance.Add(new HrLeaveBalance { HrEmployeeId = _employee.HrEmployeeId, Year = 2026, CompOffBalance = 4m });
            await _context.SaveChangesAsync();
            var leave = SeedLeaveRequest("COMP_OFF", 1m);

            await _handler.Handle(new DecideHrLeaveRequestModel
            {
                LeaveId = leave.HrLeaveRequestId,
                Status = "APPROVED",
                ApprovedByUserId = _approverId,
            }, CancellationToken.None);

            var balance = _context.HrLeaveBalance.Single(b => b.HrEmployeeId == _employee.HrEmployeeId && b.Year == 2026);
            Assert.That(balance.CompOffBalance, Is.EqualTo(3m));
            // Comp-off has no "used" counter -- only CL/SL/EL track usage separately.
            Assert.That(balance.CasualLeaveUsed, Is.EqualTo(0m));
        }

        [Test]
        public async Task Handle_Rejected_PersistsReasonAndNeverTouchesBalance()
        {
            _context.HrLeaveBalance.Add(new HrLeaveBalance { HrEmployeeId = _employee.HrEmployeeId, Year = 2026 });
            await _context.SaveChangesAsync();
            var leave = SeedLeaveRequest("CASUAL", 5m);

            var response = await _handler.Handle(new DecideHrLeaveRequestModel
            {
                LeaveId = leave.HrLeaveRequestId,
                Status = "REJECTED",
                Reason = "Insufficient staffing on requested dates",
                ApprovedByUserId = _approverId,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            var saved = _context.HrLeaveRequest.Single(l => l.HrLeaveRequestId == leave.HrLeaveRequestId);
            Assert.That(saved.Status, Is.EqualTo("REJECTED"));
            Assert.That(saved.RejectionReason, Is.EqualTo("Insufficient staffing on requested dates"));

            var balance = _context.HrLeaveBalance.Single(b => b.HrEmployeeId == _employee.HrEmployeeId && b.Year == 2026);
            Assert.That(balance.CasualLeaveBalance, Is.EqualTo(12m), "Rejected leave must never deduct from the balance");
        }

        [Test]
        public async Task Handle_LeaveNotFound_ReturnsFailureWithoutThrowing()
        {
            var response = await _handler.Handle(new DecideHrLeaveRequestModel
            {
                LeaveId = Guid.NewGuid(),
                Status = "APPROVED",
                ApprovedByUserId = _approverId,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
        }

        [Test]
        public async Task Handle_ApprovedTwice_SecondCallRefusedAndBalanceDeductedOnlyOnce()
        {
            _context.HrLeaveBalance.Add(new HrLeaveBalance { HrEmployeeId = _employee.HrEmployeeId, Year = 2026 });
            await _context.SaveChangesAsync();
            var leave = SeedLeaveRequest("CASUAL", 3m);
            var request = new DecideHrLeaveRequestModel { LeaveId = leave.HrLeaveRequestId, Status = "APPROVED", ApprovedByUserId = _approverId };

            var first = await _handler.Handle(request, CancellationToken.None);
            var second = await _handler.Handle(request, CancellationToken.None);

            Assert.That(first.Success, Is.True);
            Assert.That(second.Success, Is.False, "an already-decided request must not be decided again");
            var balance = _context.HrLeaveBalance.Single(b => b.HrEmployeeId == _employee.HrEmployeeId && b.Year == 2026);
            Assert.That(balance.CasualLeaveBalance, Is.EqualTo(9m), "12 - 3, deducted once, not twice");
            Assert.That(balance.CasualLeaveUsed, Is.EqualTo(3m));
        }

        [Test]
        public async Task Handle_RejectedLeave_CannotBeApprovedLater()
        {
            var leave = SeedLeaveRequest("SICK", 2m);
            await _handler.Handle(new DecideHrLeaveRequestModel { LeaveId = leave.HrLeaveRequestId, Status = "REJECTED", ApprovedByUserId = _approverId }, CancellationToken.None);

            var response = await _handler.Handle(new DecideHrLeaveRequestModel { LeaveId = leave.HrLeaveRequestId, Status = "APPROVED", ApprovedByUserId = _approverId }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(_context.HrLeaveRequest.Single().Status, Is.EqualTo("REJECTED"));
            Assert.That(_context.HrLeaveBalance.Any(), Is.False, "a refused decision must not create or change a balance");
        }

        [TestCase("PENDING")]
        [TestCase("approved")]
        [TestCase("")]
        [TestCase("SOMETHING_ELSE")]
        public async Task Handle_StatusOtherThanApprovedOrRejected_IsRefusedAndLeaveStaysPending(string status)
        {
            var leave = SeedLeaveRequest("CASUAL", 1m);

            var response = await _handler.Handle(new DecideHrLeaveRequestModel { LeaveId = leave.HrLeaveRequestId, Status = status, ApprovedByUserId = _approverId }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(_context.HrLeaveRequest.Single().Status, Is.EqualTo("PENDING"));
        }

        [Test]
        public async Task Handle_ApproverFromAnotherHospital_IsDeniedAndLeaveUntouched()
        {
            // A fully-permitted leave manager -- but at a DIFFERENT hospital than the employee's.
            var otherHospitalManager = HrAuthSeed.SeedMember(_context, Guid.NewGuid(), "hr.manage_leaves");
            var leave = SeedLeaveRequest("CASUAL", 2m);

            var response = await _handler.Handle(new DecideHrLeaveRequestModel { LeaveId = leave.HrLeaveRequestId, Status = "APPROVED", ApprovedByUserId = otherHospitalManager }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Leave request not found."), "same answer as a missing leave, so its existence isn't revealed");
            Assert.That(_context.HrLeaveRequest.Single().Status, Is.EqualTo("PENDING"));
        }

        [Test]
        public async Task Handle_MemberWithoutLeavePermission_IsDenied()
        {
            var plainMember = HrAuthSeed.SeedMember(_context, _employee.HospitalId);
            var leave = SeedLeaveRequest("CASUAL", 2m);

            var response = await _handler.Handle(new DecideHrLeaveRequestModel { LeaveId = leave.HrLeaveRequestId, Status = "APPROVED", ApprovedByUserId = plainMember }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(_context.HrLeaveRequest.Single().Status, Is.EqualTo("PENDING"));
        }
    }
}
