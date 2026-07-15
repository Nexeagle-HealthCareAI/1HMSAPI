using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModel;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class HospitalUpdateHandlerTests
    {
        private AppDbContext _context = null!;
        private HospitalUpdateHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new HospitalUpdateHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ValidUpdate_UpdatesHospital()
        {
            // Arrange
            var hospitalId = Guid.NewGuid();
            var hospital = new Hospital { HospitalID = hospitalId, Name = "Old Name", Email = "old@h.com", Type = "General", RegistrationNumber = "REG001", Contact = "1234567890", Location = "Test Location", City = "Test City", State = "Test State", Country = "Test Country", Pincode = "123456", CreatedByUserID = Guid.NewGuid()  };
            _context.Hospitals.Add(hospital);
            await _context.SaveChangesAsync();

            var request = new HospitalUpdateRequestModel
            {
                HospitalId = hospitalId,
                Name = "New Name",
                Email = "new@h.com"
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            
            var updatedHospital = await _context.Hospitals.FindAsync(hospitalId);
            Assert.That(updatedHospital!.Name, Is.EqualTo("New Name"));
            Assert.That(updatedHospital.Email, Is.EqualTo("new@h.com"));
        }

        [Test]
        public async Task Handle_ValidUpdate_UpdatesGeolocation()
        {
            // Arrange
            var hospitalId = Guid.NewGuid();
            var hospital = new Hospital { HospitalID = hospitalId, Name = "Old Name", Email = "old@h.com", Type = "General", RegistrationNumber = "REG001", Contact = "1234567890", Location = "Test Location", City = "Test City", State = "Test State", Country = "Test Country", Pincode = "123456", CreatedByUserID = Guid.NewGuid() };
            _context.Hospitals.Add(hospital);
            await _context.SaveChangesAsync();

            var request = new HospitalUpdateRequestModel
            {
                HospitalId = hospitalId,
                Latitude = 22.5726m,
                Longitude = 88.3639m,
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            var updatedHospital = await _context.Hospitals.FindAsync(hospitalId);
            Assert.That(updatedHospital!.Latitude, Is.EqualTo(22.5726m));
            Assert.That(updatedHospital.Longitude, Is.EqualTo(88.3639m));
        }

        [Test]
        public async Task Handle_HospitalNotFound_ReturnsFailure()
        {
            // Arrange
            var request = new HospitalUpdateRequestModel { HospitalId = Guid.NewGuid() };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Hospital not found."));
        }
    }
}
