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
    public class InfectionEventCommandHandlerTests
    {
        private AppDbContext _context = null!;
        private InfectionEventCommandHandlers _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new InfectionEventCommandHandlers(_context);
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
        public async Task Handle_ValidEvent_Succeeds()
        {
            var (hospitalId, admission) = SeedBasics();

            var response = await _handler.Handle(new LogInfectionEventRequestModel
            {
                HospitalId = hospitalId, AdmissionId = admission.AdmissionId,
                InfectionType = IpdConstants.InfectionType.Clabsi, DiagnosedByDoctorName = "Dr. House",
                CultureOrganism = "Staph aureus", LoggedInUserName = "Dr. House",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            var saved = _context.InfectionEvent.Single(e => e.InfectionEventId == response.InfectionEventId);
            Assert.That(saved.InfectionType, Is.EqualTo(IpdConstants.InfectionType.Clabsi));
        }

        [Test]
        public async Task Handle_InvalidInfectionType_ReturnsFailure()
        {
            var (hospitalId, admission) = SeedBasics();

            var response = await _handler.Handle(new LogInfectionEventRequestModel
            {
                HospitalId = hospitalId, AdmissionId = admission.AdmissionId,
                InfectionType = "NOT_REAL", DiagnosedByDoctorName = "Dr. House",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
        }

        [Test]
        public async Task Handle_DeviceFromDifferentAdmission_ReturnsFailure()
        {
            var (hospitalId, admission) = SeedBasics();
            var otherAdmissionId = Guid.NewGuid();
            _context.Admission.Add(new Admission
            {
                AdmissionId = otherAdmissionId, HospitalId = hospitalId, PatientId = "PTID00000002",
                AdmissionNo = "ADM-2", AdmittedAt = DateTime.UtcNow, StatusCode = "ADMITTED",
            });
            var device = new DeviceAssignment
            {
                DeviceAssignmentId = Guid.NewGuid(), HospitalId = hospitalId, AdmissionId = otherAdmissionId,
                DeviceType = IpdConstants.IcuDeviceType.CentralLine, InsertedByDoctorName = "Dr. House",
                InsertedAt = DateTime.UtcNow, StatusCode = IpdConstants.DeviceStatus.Active,
            };
            _context.DeviceAssignment.Add(device);
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new LogInfectionEventRequestModel
            {
                HospitalId = hospitalId, AdmissionId = admission.AdmissionId, DeviceAssignmentId = device.DeviceAssignmentId,
                InfectionType = IpdConstants.InfectionType.Clabsi, DiagnosedByDoctorName = "Dr. House",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("does not belong"));
        }
    }
}
