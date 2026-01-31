using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.Helpers.Interfaces;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
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
        private Mock<IDoctorValidationHelper> _doctorValidationHelperMock = null!;
        private DeletePersonalizedDataHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _doctorValidationHelperMock = new Mock<IDoctorValidationHelper>();
            _handler = new DeletePersonalizedDataHandler(_context, _doctorValidationHelperMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ValidData_DeletesSuccessfully()
        {
            // Arrange
            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            var hospitalId = Guid.NewGuid(); // Changed to Guid
            
            var hospital = new Hospital { HospitalID = hospitalId, Name = "Test Hospital", Email = "test@hosp.com", Type = "General", RegistrationNumber = "REG001", Contact = "1234567890", Location = "Test Location", City = "Test City", State = "Test State", Country = "Test Country", Pincode = "123456", CreatedByUserID = Guid.NewGuid()  };
            _context.Hospitals.Add(hospital);

            var personalData = new LookupPersonal
            {
                PersonalId = Guid.NewGuid(),
                DoctorID = doctor.DoctorID,
                HospitalID = hospitalId,
                LookupTypeId = 1,
                Name = "Test Note"
            };
            _context.LookupPersonals.Add(personalData);
            await _context.SaveChangesAsync();

            _doctorValidationHelperMock.Setup(x => x.ValidateDoctorAsync(hospitalId, doctor.DoctorID, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var request = new DeletePersonalizedDataRequestModel 
            { 
                PersonalId = personalData.PersonalId, 
                DoctorId = doctor.DoctorID, 
                HospitalId = hospitalId 
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            var deletedData = await _context.LookupPersonals.FindAsync(personalData.PersonalId);
            Assert.That(deletedData, Is.Null);
        }

        [Test]
        public async Task Handle_DataNotFound_ReturnsFailure()
        {
             // Arrange
            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            var hospitalId = Guid.NewGuid();
            
            var hospital = new Hospital { HospitalID = hospitalId, Name = "Test Hospital", Email = "test@hosp.com", Type = "General", RegistrationNumber = "REG001", Contact = "1234567890", Location = "Test Location", City = "Test City", State = "Test State", Country = "Test Country", Pincode = "123456", CreatedByUserID = Guid.NewGuid()  };
            _context.Hospitals.Add(hospital);
            await _context.SaveChangesAsync();
            
             _doctorValidationHelperMock.Setup(x => x.ValidateDoctorAsync(hospitalId, doctor.DoctorID, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var request = new DeletePersonalizedDataRequestModel 
            { 
                PersonalId = Guid.NewGuid(),
                DoctorId = doctor.DoctorID,
                HospitalId = hospitalId
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Personalized data not found."));
        }
    }
}
