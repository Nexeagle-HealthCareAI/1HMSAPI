using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.Application.Helpers.Interfaces;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class DeletePersonalizedDataHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<IDoctorValidationHelper> _mockDoctorValidationHelper = null!;
        private DeletePersonalizedDataHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _mockDoctorValidationHelper = new Mock<IDoctorValidationHelper>();
            _handler = new DeletePersonalizedDataHandler(_context, _mockDoctorValidationHelper.Object);
        }

        [TearDown]
        public void TearDown()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        [Test]
        public async Task Handle_DoctorNotFound_ReturnsError()
        {
            // Arrange
            var request = new DeletePersonalizedDataRequestModel
            {
                DoctorId = Guid.NewGuid(),
                HospitalId = Guid.NewGuid(),
                PersonalId = Guid.NewGuid()
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Doctor not found."));
        }

        [Test]
        public async Task Handle_HospitalNotFound_ReturnsError()
        {
            // Arrange
            var doctorId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            _context.Users.Add(TestEntityFactory.CreateUser(userId));
            _context.Doctors.Add(TestEntityFactory.CreateDoctor(doctorId, userId));
            await _context.SaveChangesAsync();

            var request = new DeletePersonalizedDataRequestModel
            {
                DoctorId = doctorId,
                HospitalId = Guid.NewGuid(),
                PersonalId = Guid.NewGuid()
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Hospital not found."));
        }

        [Test]
        public async Task Handle_ValidationFailed_ReturnsError()
        {
             // Arrange
            var doctorId = Guid.NewGuid();
            var hospitalId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            
            _context.Users.Add(TestEntityFactory.CreateUser(userId));
            _context.Doctors.Add(TestEntityFactory.CreateDoctor(doctorId, userId));
            _context.Hospitals.Add(TestEntityFactory.CreateHospital(hospitalId, userId));
            await _context.SaveChangesAsync();
            
            _mockDoctorValidationHelper.Setup(x => x.ValidateDoctorAsync(hospitalId, doctorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var request = new DeletePersonalizedDataRequestModel
            {
                DoctorId = doctorId,
                HospitalId = hospitalId,
                PersonalId = Guid.NewGuid()
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Doctor is not associated with the specified hospital."));
        }

        [Test]
        public async Task Handle_PersonalizedDataNotFound_ReturnsError()
        {
             // Arrange
            var doctorId = Guid.NewGuid();
            var hospitalId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            
            _context.Users.Add(TestEntityFactory.CreateUser(userId));
            _context.Doctors.Add(TestEntityFactory.CreateDoctor(doctorId, userId));
            _context.Hospitals.Add(TestEntityFactory.CreateHospital(hospitalId, userId));
            await _context.SaveChangesAsync();

            _mockDoctorValidationHelper.Setup(x => x.ValidateDoctorAsync(hospitalId, doctorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var request = new DeletePersonalizedDataRequestModel
            {
                DoctorId = doctorId,
                HospitalId = hospitalId,
                PersonalId = Guid.NewGuid()
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Personalized data not found."));
        }

        [Test]
        public async Task Handle_Success_DeletesPersonalizedData()
        {
            // Arrange
            var doctorId = Guid.NewGuid();
            var hospitalId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var personalId = Guid.NewGuid();

            _context.Users.Add(TestEntityFactory.CreateUser(userId));
            _context.Doctors.Add(TestEntityFactory.CreateDoctor(doctorId, userId));
            _context.Hospitals.Add(TestEntityFactory.CreateHospital(hospitalId, userId));
            _context.LookupPersonals.Add(TestEntityFactory.CreateLookupPersonal(personalId, hospitalId, doctorId));
            await _context.SaveChangesAsync();

            _mockDoctorValidationHelper.Setup(x => x.ValidateDoctorAsync(hospitalId, doctorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var request = new DeletePersonalizedDataRequestModel
            {
                DoctorId = doctorId,
                HospitalId = hospitalId,
                PersonalId = personalId
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.Message, Is.EqualTo("Personalized data deleted successfully."));
            
            var deletedData = await _context.LookupPersonals.FindAsync(personalId);
            Assert.That(deletedData, Is.Null);
        }
    }
}
