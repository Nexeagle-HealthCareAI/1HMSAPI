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
    public class DeviceAssignmentCommandHandlerTests
    {
        private AppDbContext _context = null!;
        private DeviceAssignmentCommandHandlers _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new DeviceAssignmentCommandHandlers(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private (Guid hospitalId, Admission admission) SeedBasics()
        {
            var hospitalId = Guid.NewGuid();
            _context.Hospitals.Add(new Hospital
            {
                HospitalID = hospitalId, Name = "Hosp", Email = "e@m.com", Type = "General", RegistrationNumber = "REG001",
                Contact = "1234567890", Location = "Test Location", City = "Test City", State = "Test State",
                Country = "Test Country", Pincode = "123456", CreatedByUserID = Guid.NewGuid(),
            });
            var admission = new Admission
            {
                AdmissionId = Guid.NewGuid(), HospitalId = hospitalId, PatientId = "PTID00000001",
                AdmissionNo = "ADM-1", AdmittedAt = DateTime.UtcNow, StatusCode = "ADMITTED",
            };
            _context.Admission.Add(admission);
            _context.SaveChanges();
            return (hospitalId, admission);
        }

        [Test]
        public async Task Handle_Insert_Valid_Succeeds()
        {
            var (hospitalId, admission) = SeedBasics();

            var response = await _handler.Handle(new InsertDeviceRequestModel
            {
                HospitalId = hospitalId, AdmissionId = admission.AdmissionId,
                DeviceType = IpdConstants.IcuDeviceType.CentralLine, InsertedByDoctorName = "Dr. House",
                InsertionSite = "Right IJ", LoggedInUserName = "Nurse Joy",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            Assert.That(response.DeviceAssignmentId, Is.Not.Null);
            var saved = _context.DeviceAssignment.Single(d => d.DeviceAssignmentId == response.DeviceAssignmentId);
            Assert.That(saved.StatusCode, Is.EqualTo(IpdConstants.DeviceStatus.Active));
        }

        [Test]
        public async Task Handle_Insert_DuplicateActiveSameType_ReturnsFailure()
        {
            var (hospitalId, admission) = SeedBasics();
            await _handler.Handle(new InsertDeviceRequestModel
            {
                HospitalId = hospitalId, AdmissionId = admission.AdmissionId,
                DeviceType = IpdConstants.IcuDeviceType.CentralLine, InsertedByDoctorName = "Dr. House",
            }, CancellationToken.None);

            var second = await _handler.Handle(new InsertDeviceRequestModel
            {
                HospitalId = hospitalId, AdmissionId = admission.AdmissionId,
                DeviceType = IpdConstants.IcuDeviceType.CentralLine, InsertedByDoctorName = "Dr. Wilson",
            }, CancellationToken.None);

            Assert.That(second.Success, Is.False);
            Assert.That(second.Message, Does.Contain("already has an active device"));
        }

        [Test]
        public async Task Handle_Insert_DifferentDeviceTypesConcurrently_Succeeds()
        {
            var (hospitalId, admission) = SeedBasics();
            var centralLine = await _handler.Handle(new InsertDeviceRequestModel
            {
                HospitalId = hospitalId, AdmissionId = admission.AdmissionId,
                DeviceType = IpdConstants.IcuDeviceType.CentralLine, InsertedByDoctorName = "Dr. House",
            }, CancellationToken.None);

            var catheter = await _handler.Handle(new InsertDeviceRequestModel
            {
                HospitalId = hospitalId, AdmissionId = admission.AdmissionId,
                DeviceType = IpdConstants.IcuDeviceType.UrinaryCatheter, InsertedByDoctorName = "Dr. House",
            }, CancellationToken.None);

            Assert.That(centralLine.Success, Is.True, centralLine.Message);
            Assert.That(catheter.Success, Is.True, catheter.Message);
            Assert.That(_context.DeviceAssignment.Count(d => d.AdmissionId == admission.AdmissionId && d.StatusCode == IpdConstants.DeviceStatus.Active), Is.EqualTo(2));
        }

        [Test]
        public async Task Handle_Insert_InvalidDeviceType_ReturnsFailure()
        {
            var (hospitalId, admission) = SeedBasics();

            var response = await _handler.Handle(new InsertDeviceRequestModel
            {
                HospitalId = hospitalId, AdmissionId = admission.AdmissionId,
                DeviceType = "NOT_REAL", InsertedByDoctorName = "Dr. House",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
        }

        [Test]
        public async Task Handle_Remove_ActiveDevice_Succeeds()
        {
            var (hospitalId, admission) = SeedBasics();
            var insert = await _handler.Handle(new InsertDeviceRequestModel
            {
                HospitalId = hospitalId, AdmissionId = admission.AdmissionId,
                DeviceType = IpdConstants.IcuDeviceType.Ett, InsertedByDoctorName = "Dr. House",
            }, CancellationToken.None);

            var remove = await _handler.Handle(new RemoveDeviceRequestModel
            {
                HospitalId = hospitalId, DeviceAssignmentId = insert.DeviceAssignmentId!.Value, RemovalReason = "Extubated",
            }, CancellationToken.None);

            Assert.That(remove.Success, Is.True, remove.Message);
            var saved = _context.DeviceAssignment.Single(d => d.DeviceAssignmentId == insert.DeviceAssignmentId);
            Assert.That(saved.StatusCode, Is.EqualTo(IpdConstants.DeviceStatus.Removed));
            Assert.That(saved.RemovedAt, Is.Not.Null);
        }

        [Test]
        public async Task Handle_Remove_AlreadyRemoved_ReturnsFailure()
        {
            var (hospitalId, admission) = SeedBasics();
            var insert = await _handler.Handle(new InsertDeviceRequestModel
            {
                HospitalId = hospitalId, AdmissionId = admission.AdmissionId,
                DeviceType = IpdConstants.IcuDeviceType.Ett, InsertedByDoctorName = "Dr. House",
            }, CancellationToken.None);
            await _handler.Handle(new RemoveDeviceRequestModel { HospitalId = hospitalId, DeviceAssignmentId = insert.DeviceAssignmentId!.Value }, CancellationToken.None);

            var secondRemove = await _handler.Handle(new RemoveDeviceRequestModel { HospitalId = hospitalId, DeviceAssignmentId = insert.DeviceAssignmentId!.Value }, CancellationToken.None);

            Assert.That(secondRemove.Success, Is.False);
            Assert.That(secondRemove.Message, Does.Contain("already removed"));
        }

        [Test]
        public async Task Handle_Insert_UnknownAdmission_ReturnsFailure()
        {
            var (hospitalId, _) = SeedBasics();

            var response = await _handler.Handle(new InsertDeviceRequestModel
            {
                HospitalId = hospitalId, AdmissionId = Guid.NewGuid(),
                DeviceType = IpdConstants.IcuDeviceType.CentralLine, InsertedByDoctorName = "Dr. House",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("not found"));
        }
    }
}
