using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class NurseShiftAssignmentCommandHandlerTests
    {
        private AppDbContext _context = null!;
        private NurseShiftAssignmentCommandHandlers _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new NurseShiftAssignmentCommandHandlers(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private (Guid hospitalId, User nurse1, User nurse2, string wardCode) SeedBasics()
        {
            var admin = TestDataFactory.SeedUser(_context, email: "admin@example.com", phone: "9000000000");
            var hospital = TestDataFactory.SeedHospital(_context, admin.UserID);

            var nurse1 = TestDataFactory.SeedUser(_context, email: "nurse1@example.com", phone: "1111111111", role: "Nurse");
            var nurse2 = TestDataFactory.SeedUser(_context, email: "nurse2@example.com", phone: "2222222222", role: "Nurse");

            _context.HospitalUsers.Add(new HospitalUser { HospitalUserID = Guid.NewGuid(), HospitalID = hospital.HospitalID, UserID = nurse1.UserID });
            _context.HospitalUsers.Add(new HospitalUser { HospitalUserID = Guid.NewGuid(), HospitalID = hospital.HospitalID, UserID = nurse2.UserID });

            var wardCode = "GEN-A";
            _context.BedMaster.Add(new BedMaster
            {
                BedId = Guid.NewGuid(),
                HospitalId = hospital.HospitalID,
                WardCode = wardCode,
                WardName = "General Ward A",
                IsActive = true,
                WardRoomDailyRate = 1000,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            _context.SaveChanges();

            return (hospital.HospitalID, nurse1, nurse2, wardCode);
        }

        [Test]
        public async Task Handle_ValidRequest_CreatesActiveAssignment()
        {
            var (hospitalId, nurse1, _, wardCode) = SeedBasics();

            var response = await _handler.Handle(new AssignNurseShiftRequestModel
            {
                HospitalId = hospitalId, NurseUserId = nurse1.UserID, WardCode = wardCode, ShiftCode = "morning", LoggedInUserName = "Admin",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            var row = _context.NurseShiftAssignment.Single(a => a.NurseShiftAssignmentId == response.NurseShiftAssignmentId);
            Assert.That(row.ShiftCode, Is.EqualTo("MORNING"));
            Assert.That(row.StatusCode, Is.EqualTo(IpdConstants.NurseAssignmentStatus.Active));
            Assert.That(row.ShiftDate, Is.Null);
        }

        [Test]
        public async Task Handle_DifferentNurse_SameWardShift_Succeeds_TeamModel()
        {
            var (hospitalId, nurse1, nurse2, wardCode) = SeedBasics();

            var first = await _handler.Handle(new AssignNurseShiftRequestModel
            {
                HospitalId = hospitalId, NurseUserId = nurse1.UserID, WardCode = wardCode, ShiftCode = "MORNING", LoggedInUserName = "Admin",
            }, CancellationToken.None);
            var second = await _handler.Handle(new AssignNurseShiftRequestModel
            {
                HospitalId = hospitalId, NurseUserId = nurse2.UserID, WardCode = wardCode, ShiftCode = "MORNING", LoggedInUserName = "Admin",
            }, CancellationToken.None);

            Assert.That(first.Success, Is.True, first.Message);
            Assert.That(second.Success, Is.True, second.Message);
            Assert.That(_context.NurseShiftAssignment.Count(a => a.WardCode == wardCode && a.ShiftCode == "MORNING"), Is.EqualTo(2));
        }

        [Test]
        public async Task Handle_SameNurse_SameWardShiftDate_ReturnsFailure()
        {
            var (hospitalId, nurse1, _, wardCode) = SeedBasics();

            var first = await _handler.Handle(new AssignNurseShiftRequestModel
            {
                HospitalId = hospitalId, NurseUserId = nurse1.UserID, WardCode = wardCode, ShiftCode = "MORNING", LoggedInUserName = "Admin",
            }, CancellationToken.None);
            var second = await _handler.Handle(new AssignNurseShiftRequestModel
            {
                HospitalId = hospitalId, NurseUserId = nurse1.UserID, WardCode = wardCode, ShiftCode = "MORNING", LoggedInUserName = "Admin",
            }, CancellationToken.None);

            Assert.That(first.Success, Is.True, first.Message);
            Assert.That(second.Success, Is.False);
            Assert.That(second.Message, Does.Contain("already rostered"));
        }

        [Test]
        public async Task Handle_InvalidShiftCode_ReturnsFailure()
        {
            var (hospitalId, nurse1, _, wardCode) = SeedBasics();

            var response = await _handler.Handle(new AssignNurseShiftRequestModel
            {
                HospitalId = hospitalId, NurseUserId = nurse1.UserID, WardCode = wardCode, ShiftCode = "AFTERNOON", LoggedInUserName = "Admin",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("Invalid shift code"));
        }

        [Test]
        public async Task Handle_WardNotFound_ReturnsFailure()
        {
            var (hospitalId, nurse1, _, _) = SeedBasics();

            var response = await _handler.Handle(new AssignNurseShiftRequestModel
            {
                HospitalId = hospitalId, NurseUserId = nurse1.UserID, WardCode = "NOPE", ShiftCode = "MORNING", LoggedInUserName = "Admin",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("Ward not found"));
        }

        [Test]
        public async Task Handle_NurseNotInHospital_ReturnsFailure()
        {
            var (hospitalId, _, _, wardCode) = SeedBasics();
            var stranger = TestDataFactory.SeedUser(_context, email: "stranger@example.com", phone: "3333333333", role: "Nurse");

            var response = await _handler.Handle(new AssignNurseShiftRequestModel
            {
                HospitalId = hospitalId, NurseUserId = stranger.UserID, WardCode = wardCode, ShiftCode = "MORNING", LoggedInUserName = "Admin",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("does not belong"));
        }

        [Test]
        public async Task Handle_Release_FlipsRowToReleased()
        {
            var (hospitalId, nurse1, _, wardCode) = SeedBasics();
            var assign = await _handler.Handle(new AssignNurseShiftRequestModel
            {
                HospitalId = hospitalId, NurseUserId = nurse1.UserID, WardCode = wardCode, ShiftCode = "MORNING", LoggedInUserName = "Admin",
            }, CancellationToken.None);

            var release = await _handler.Handle(new ReleaseNurseShiftRequestModel
            {
                HospitalId = hospitalId, NurseShiftAssignmentId = assign.NurseShiftAssignmentId!.Value, LoggedInUserName = "Admin",
            }, CancellationToken.None);

            Assert.That(release.Success, Is.True, release.Message);
            var row = _context.NurseShiftAssignment.Single(a => a.NurseShiftAssignmentId == assign.NurseShiftAssignmentId);
            Assert.That(row.StatusCode, Is.EqualTo(IpdConstants.NurseAssignmentStatus.Released));
            Assert.That(row.UnassignedAt, Is.Not.Null);
            Assert.That(row.UnassignedBy, Is.EqualTo("Admin"));
        }

        [Test]
        public async Task Handle_ReleaseAlreadyReleased_ReturnsFailure()
        {
            var (hospitalId, nurse1, _, wardCode) = SeedBasics();
            var assign = await _handler.Handle(new AssignNurseShiftRequestModel
            {
                HospitalId = hospitalId, NurseUserId = nurse1.UserID, WardCode = wardCode, ShiftCode = "MORNING", LoggedInUserName = "Admin",
            }, CancellationToken.None);
            await _handler.Handle(new ReleaseNurseShiftRequestModel
            {
                HospitalId = hospitalId, NurseShiftAssignmentId = assign.NurseShiftAssignmentId!.Value, LoggedInUserName = "Admin",
            }, CancellationToken.None);

            var secondRelease = await _handler.Handle(new ReleaseNurseShiftRequestModel
            {
                HospitalId = hospitalId, NurseShiftAssignmentId = assign.NurseShiftAssignmentId!.Value, LoggedInUserName = "Admin",
            }, CancellationToken.None);

            Assert.That(secondRelease.Success, Is.False);
            Assert.That(secondRelease.Message, Does.Contain("already released"));
        }

        [Test]
        public async Task Handle_ReleaseNotFound_ReturnsFailure()
        {
            var (hospitalId, _, _, _) = SeedBasics();

            var response = await _handler.Handle(new ReleaseNurseShiftRequestModel
            {
                HospitalId = hospitalId, NurseShiftAssignmentId = Guid.NewGuid(), LoggedInUserName = "Admin",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("not found"));
        }
    }
}
