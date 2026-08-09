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
    public class PatientNurseAssignmentCommandHandlerTests
    {
        private AppDbContext _context = null!;
        private PatientNurseAssignmentCommandHandlers _handler = null!;
        private Guid _hospitalId;
        private Guid _admissionId;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new PatientNurseAssignmentCommandHandlers(_context);
            _hospitalId = Guid.NewGuid();
            _admissionId = Guid.NewGuid();
        }

        [TearDown]
        public void TearDown()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        private void SeedAdmission()
        {
            _context.Admission.Add(new Admission
            {
                AdmissionId = _admissionId,
                HospitalId = _hospitalId,
                PatientId = "PT001",
                AdmissionNo = "ADM-1",
                AdmittedAt = DateTime.UtcNow,
                StatusCode = "ADMITTED",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
        }

        private Guid SeedNurseInHospital()
        {
            var userId = Guid.NewGuid();
            _context.Users.Add(TestEntityFactory.CreateUser(userId));
            _context.HospitalUsers.Add(new HospitalUser { HospitalID = _hospitalId, UserID = userId });
            return userId;
        }

        private AssignPatientNurseRequestModel ValidRequest(Guid nurseUserId) => new()
        {
            HospitalId = _hospitalId,
            AdmissionId = _admissionId,
            NurseUserId = nurseUserId,
            ShiftCode = "MORNING",
        };

        [Test]
        public async Task Handle_MissingFields_ReturnsError()
        {
            var response = await _handler.Handle(new AssignPatientNurseRequestModel(), CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("required"));
        }

        [Test]
        public async Task Handle_InvalidShiftCode_ReturnsError()
        {
            SeedAdmission();
            var nurseId = SeedNurseInHospital();
            await _context.SaveChangesAsync();

            var request = ValidRequest(nurseId);
            request.ShiftCode = "AFTERNOON";

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("shift"));
        }

        [Test]
        public async Task Handle_AdmissionNotFound_ReturnsError()
        {
            var nurseId = SeedNurseInHospital();
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(ValidRequest(nurseId), CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("Admission not found"));
        }

        [Test]
        public async Task Handle_NurseNotInHospital_ReturnsError()
        {
            SeedAdmission();
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(ValidRequest(Guid.NewGuid()), CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("does not belong"));
        }

        [Test]
        public async Task Handle_ValidRequest_AssignsNurse()
        {
            SeedAdmission();
            var nurseId = SeedNurseInHospital();
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(ValidRequest(nurseId), CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.PatientNurseAssignmentId, Is.Not.Null);
            Assert.That(_context.PatientNurseAssignment.Count(a => a.AdmissionId == _admissionId && a.NurseUserId == nurseId), Is.EqualTo(1));
        }

        [Test]
        public async Task Handle_SameNurseTwiceForSameShift_ReturnsError()
        {
            SeedAdmission();
            var nurseId = SeedNurseInHospital();
            await _context.SaveChangesAsync();

            var first = await _handler.Handle(ValidRequest(nurseId), CancellationToken.None);
            Assert.That(first.Success, Is.True);

            var second = await _handler.Handle(ValidRequest(nurseId), CancellationToken.None);

            Assert.That(second.Success, Is.False);
            Assert.That(second.Message, Does.Contain("already assigned"));
        }

        [Test]
        public async Task Handle_TwoDifferentNurses_SamePatientSameShift_BothSucceed()
        {
            SeedAdmission();
            var nurse1 = SeedNurseInHospital();
            var nurse2 = SeedNurseInHospital();
            await _context.SaveChangesAsync();

            var first = await _handler.Handle(ValidRequest(nurse1), CancellationToken.None);
            var second = await _handler.Handle(ValidRequest(nurse2), CancellationToken.None);

            Assert.That(first.Success, Is.True);
            Assert.That(second.Success, Is.True);
            Assert.That(_context.PatientNurseAssignment.Count(a => a.AdmissionId == _admissionId && a.StatusCode == "ACTIVE"), Is.EqualTo(2));
        }

        [Test]
        public async Task Handle_Release_FlipsToReleased()
        {
            SeedAdmission();
            var nurseId = SeedNurseInHospital();
            await _context.SaveChangesAsync();
            var assignResponse = await _handler.Handle(ValidRequest(nurseId), CancellationToken.None);

            var response = await _handler.Handle(new ReleasePatientNurseRequestModel
            {
                HospitalId = _hospitalId,
                PatientNurseAssignmentId = assignResponse.PatientNurseAssignmentId!.Value,
                LoggedInUserName = "charge_nurse",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            var reloaded = _context.PatientNurseAssignment.First(a => a.PatientNurseAssignmentId == assignResponse.PatientNurseAssignmentId);
            Assert.That(reloaded.StatusCode, Is.EqualTo("RELEASED"));
            Assert.That(reloaded.UnassignedAt, Is.Not.Null);
            Assert.That(reloaded.UnassignedBy, Is.EqualTo("charge_nurse"));
        }

        [Test]
        public async Task Handle_ReleaseAlreadyReleased_ReturnsError()
        {
            SeedAdmission();
            var nurseId = SeedNurseInHospital();
            await _context.SaveChangesAsync();
            var assignResponse = await _handler.Handle(ValidRequest(nurseId), CancellationToken.None);
            await _handler.Handle(new ReleasePatientNurseRequestModel { HospitalId = _hospitalId, PatientNurseAssignmentId = assignResponse.PatientNurseAssignmentId!.Value }, CancellationToken.None);

            var response = await _handler.Handle(new ReleasePatientNurseRequestModel
            {
                HospitalId = _hospitalId,
                PatientNurseAssignmentId = assignResponse.PatientNurseAssignmentId!.Value,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("already released"));
        }
    }
}
