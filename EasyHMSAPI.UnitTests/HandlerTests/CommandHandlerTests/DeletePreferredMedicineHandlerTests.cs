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
    public class DeletePreferredMedicineHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<IDoctorValidationHelper> _doctorValidationHelperMock = null!;
        private DeletePreferredMedicineHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _doctorValidationHelperMock = new Mock<IDoctorValidationHelper>();
            _handler = new DeletePreferredMedicineHandler(_context, _doctorValidationHelperMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ValidRequest_DeletesMedicine()
        {
            // Arrange
            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            var hospitalId = Guid.NewGuid(); // Changed to Guid
            var hospital = new Hospital { HospitalID = hospitalId, Name = "Test Hospital", Email = "test@hosp.com", Type = "General", RegistrationNumber = "REG001", Contact = "1234567890", Location = "Test Location", City = "Test City", State = "Test State", Country = "Test Country", Pincode = "123456", CreatedByUserID = Guid.NewGuid()  };
            _context.Hospitals.Add(hospital);

            var medicine = new DoctorPreferredMedicine
            {
                PreferrredId = 1,
                DoctorId = doctor.DoctorID,
                HospitalId = hospitalId,
                MedicineName = "Test Med",
                GenericName = "Generic Test Med"
            };
            _context.DoctorPreferredMedicines.Add(medicine);
            await _context.SaveChangesAsync();

            _doctorValidationHelperMock.Setup(x => x.ValidateDoctorAsync(hospitalId, doctor.DoctorID, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var request = new DeletePreferredMedicineRequestModel 
            { 
                PreferredId = 1,
                DoctorId = doctor.DoctorID,
                HospitalId = hospitalId
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            var deleted = await _context.DoctorPreferredMedicines.FindAsync(medicine.PreferrredId);
            Assert.That(deleted, Is.Null);
        }

        [Test]
        public async Task Handle_MedicineNotFound_ReturnsFailure()
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

            var request = new DeletePreferredMedicineRequestModel 
            { 
                 PreferredId = 1,
                 DoctorId = doctor.DoctorID,
                 HospitalId = hospitalId
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Preferred medicine not found."));
        }
    }
}
