using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class GetDeviceAssignmentsHandlerTests
    {
        private AppDbContext _context = null!;
        private GetDeviceAssignmentsHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetDeviceAssignmentsHandler(_context);
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
        public async Task Handle_ActiveFirst_ThenHistoryByInsertedAtDesc()
        {
            var (hospitalId, admission) = SeedBasics();
            _context.DeviceAssignment.Add(new DeviceAssignment
            {
                DeviceAssignmentId = Guid.NewGuid(), HospitalId = hospitalId, AdmissionId = admission.AdmissionId,
                DeviceType = IpdConstants.IcuDeviceType.Ett, InsertedByDoctorName = "Dr. House",
                InsertedAt = DateTime.UtcNow.AddDays(-3), RemovedAt = DateTime.UtcNow.AddDays(-1), StatusCode = IpdConstants.DeviceStatus.Removed,
            });
            _context.DeviceAssignment.Add(new DeviceAssignment
            {
                DeviceAssignmentId = Guid.NewGuid(), HospitalId = hospitalId, AdmissionId = admission.AdmissionId,
                DeviceType = IpdConstants.IcuDeviceType.CentralLine, InsertedByDoctorName = "Dr. House",
                InsertedAt = DateTime.UtcNow.AddDays(-2), StatusCode = IpdConstants.DeviceStatus.Active,
            });
            await _context.SaveChangesAsync();

            var result = await _handler.Handle(new GetDeviceAssignmentsRequestModel { HospitalId = hospitalId, AdmissionId = admission.AdmissionId }, CancellationToken.None);

            Assert.That(result.Devices, Has.Count.EqualTo(2));
            Assert.That(result.Devices[0].StatusCode, Is.EqualTo(IpdConstants.DeviceStatus.Active));
            Assert.That(result.Devices[0].DeviceType, Is.EqualTo(IpdConstants.IcuDeviceType.CentralLine));
        }

        [Test]
        public async Task Handle_DaysInSitu_ComputedFromInsertedAtToRemovedAt()
        {
            var (hospitalId, admission) = SeedBasics();
            _context.DeviceAssignment.Add(new DeviceAssignment
            {
                DeviceAssignmentId = Guid.NewGuid(), HospitalId = hospitalId, AdmissionId = admission.AdmissionId,
                DeviceType = IpdConstants.IcuDeviceType.UrinaryCatheter, InsertedByDoctorName = "Dr. House",
                InsertedAt = DateTime.UtcNow.AddDays(-5), RemovedAt = DateTime.UtcNow.AddDays(-2), StatusCode = IpdConstants.DeviceStatus.Removed,
            });
            await _context.SaveChangesAsync();

            var result = await _handler.Handle(new GetDeviceAssignmentsRequestModel { HospitalId = hospitalId, AdmissionId = admission.AdmissionId }, CancellationToken.None);

            Assert.That(result.Devices, Has.Count.EqualTo(1));
            Assert.That(result.Devices[0].DaysInSitu, Is.EqualTo(3));
        }
    }
}
