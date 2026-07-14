using System;
using System.Collections.Generic;
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
    public class DeviceCareBundleCheckCommandHandlerTests
    {
        private AppDbContext _context = null!;
        private DeviceCareBundleCheckCommandHandlers _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new DeviceCareBundleCheckCommandHandlers(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private (Guid hospitalId, DeviceAssignment device) SeedActiveDevice(string deviceType = "CENTRAL_LINE", string statusCode = "ACTIVE")
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
                DeviceType = deviceType, InsertedByDoctorName = "Dr. House", InsertedAt = DateTime.UtcNow, StatusCode = statusCode,
            };
            _context.DeviceAssignment.Add(device);
            _context.SaveChanges();
            return (hospitalId, device);
        }

        private List<CareBundleItemResult> AllCompliantItems(string deviceType) =>
            IpdConstants.CareBundleItems.All[deviceType].Select(i => new CareBundleItemResult { Key = i.Key, Compliant = true }).ToList();

        [Test]
        public async Task Handle_ValidFullyCompliantCheck_Succeeds()
        {
            var (hospitalId, device) = SeedActiveDevice();

            var response = await _handler.Handle(new SubmitDeviceCareBundleCheckRequestModel
            {
                HospitalId = hospitalId, DeviceAssignmentId = device.DeviceAssignmentId,
                Items = AllCompliantItems(IpdConstants.IcuDeviceType.CentralLine), LoggedInUserName = "Nurse Joy",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            Assert.That(response.AllCompliant, Is.True);
            Assert.That(response.CompliantCount, Is.EqualTo(response.TotalItems));
        }

        [Test]
        public async Task Handle_PartiallyCompliantCheck_ComputesCountServerSide()
        {
            var (hospitalId, device) = SeedActiveDevice();
            var items = AllCompliantItems(IpdConstants.IcuDeviceType.CentralLine);
            items[0].Compliant = false;

            var response = await _handler.Handle(new SubmitDeviceCareBundleCheckRequestModel
            {
                HospitalId = hospitalId, DeviceAssignmentId = device.DeviceAssignmentId, Items = items,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            Assert.That(response.AllCompliant, Is.False);
            Assert.That(response.CompliantCount, Is.EqualTo(items.Count - 1));
        }

        [Test]
        public async Task Handle_MismatchedItemKeys_ReturnsFailure()
        {
            var (hospitalId, device) = SeedActiveDevice();

            var response = await _handler.Handle(new SubmitDeviceCareBundleCheckRequestModel
            {
                HospitalId = hospitalId, DeviceAssignmentId = device.DeviceAssignmentId,
                Items = new List<CareBundleItemResult> { new CareBundleItemResult { Key = "not_a_real_item", Compliant = true } },
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("do not match"));
        }

        [Test]
        public async Task Handle_RemovedDevice_ReturnsFailure()
        {
            var (hospitalId, device) = SeedActiveDevice(statusCode: IpdConstants.DeviceStatus.Removed);

            var response = await _handler.Handle(new SubmitDeviceCareBundleCheckRequestModel
            {
                HospitalId = hospitalId, DeviceAssignmentId = device.DeviceAssignmentId,
                Items = AllCompliantItems(IpdConstants.IcuDeviceType.CentralLine),
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("removed device"));
        }

        [Test]
        public async Task Handle_UnknownDevice_ReturnsFailure()
        {
            var (hospitalId, _) = SeedActiveDevice();

            var response = await _handler.Handle(new SubmitDeviceCareBundleCheckRequestModel
            {
                HospitalId = hospitalId, DeviceAssignmentId = Guid.NewGuid(),
                Items = AllCompliantItems(IpdConstants.IcuDeviceType.CentralLine),
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("not found"));
        }
    }
}
