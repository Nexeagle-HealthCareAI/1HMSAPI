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
    public class GetDeviceCareBundleChecksHandlerTests
    {
        private AppDbContext _context = null!;
        private GetDeviceCareBundleChecksHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetDeviceCareBundleChecksHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private (Guid hospitalId, DeviceAssignment device) SeedActiveDevice()
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
            var device = new DeviceAssignment
            {
                DeviceAssignmentId = Guid.NewGuid(), HospitalId = hospitalId, AdmissionId = admission.AdmissionId,
                DeviceType = IpdConstants.IcuDeviceType.UrinaryCatheter, InsertedByDoctorName = "Dr. House",
                InsertedAt = DateTime.UtcNow, StatusCode = IpdConstants.DeviceStatus.Active,
            };
            _context.DeviceAssignment.Add(device);
            _context.SaveChanges();
            return (hospitalId, device);
        }

        [Test]
        public async Task Handle_ReturnsCanonicalItems_ForDeviceType()
        {
            var (hospitalId, device) = SeedActiveDevice();

            var result = await _handler.Handle(new GetDeviceCareBundleChecksRequestModel { HospitalId = hospitalId, DeviceAssignmentId = device.DeviceAssignmentId }, CancellationToken.None);

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.CanonicalItems, Has.Count.EqualTo(IpdConstants.CareBundleItems.All[IpdConstants.IcuDeviceType.UrinaryCatheter].Length));
        }

        [Test]
        public async Task Handle_ReturnsChecks_NewestFirst_WithDeserializedItems()
        {
            var (hospitalId, device) = SeedActiveDevice();
            _context.DeviceCareBundleCheck.Add(new DeviceCareBundleCheck
            {
                CheckId = Guid.NewGuid(), HospitalId = hospitalId, AdmissionId = device.AdmissionId, DeviceAssignmentId = device.DeviceAssignmentId,
                DeviceType = device.DeviceType, ItemsJson = "[{\"Key\":\"catheter_indicated\",\"Compliant\":true}]",
                CompliantCount = 1, TotalItems = 5, AllCompliant = false, CheckedBy = "Nurse Joy", CheckedAt = DateTime.UtcNow.AddHours(-2),
            });
            _context.DeviceCareBundleCheck.Add(new DeviceCareBundleCheck
            {
                CheckId = Guid.NewGuid(), HospitalId = hospitalId, AdmissionId = device.AdmissionId, DeviceAssignmentId = device.DeviceAssignmentId,
                DeviceType = device.DeviceType, ItemsJson = "[{\"Key\":\"catheter_indicated\",\"Compliant\":true}]",
                CompliantCount = 5, TotalItems = 5, AllCompliant = true, CheckedBy = "Nurse Joy", CheckedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();

            var result = await _handler.Handle(new GetDeviceCareBundleChecksRequestModel { HospitalId = hospitalId, DeviceAssignmentId = device.DeviceAssignmentId }, CancellationToken.None);

            Assert.That(result.Checks, Has.Count.EqualTo(2));
            Assert.That(result.Checks[0].AllCompliant, Is.True);
            Assert.That(result.Checks[0].Items, Has.Count.EqualTo(1));
            Assert.That(result.Checks[0].Items[0].Key, Is.EqualTo("catheter_indicated"));
        }
    }
}
