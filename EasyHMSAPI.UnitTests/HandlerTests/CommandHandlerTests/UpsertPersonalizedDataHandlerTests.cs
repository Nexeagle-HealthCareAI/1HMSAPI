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
    public class UpsertPersonalizedDataHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<IDoctorValidationHelper> _mockDoctorValidationHelper = null!;
        private UpsertPersonalizedDataHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _mockDoctorValidationHelper = new Mock<IDoctorValidationHelper>();
            _handler = new UpsertPersonalizedDataHandler(_context, _mockDoctorValidationHelper.Object);
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
            var request = new UpsertPersonalizedDataRequestModel
            {
                DoctorId = Guid.NewGuid(),
                HospitalId = Guid.NewGuid()
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

            var request = new UpsertPersonalizedDataRequestModel
            {
                DoctorId = doctorId,
                HospitalId = Guid.NewGuid()
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

            var request = new UpsertPersonalizedDataRequestModel
            {
                DoctorId = doctorId,
                HospitalId = hospitalId
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Doctor is not associated with the specified hospital."));
        }

        [Test]
        public async Task Handle_LookupTypeNotFound_ReturnsError()
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

            var request = new UpsertPersonalizedDataRequestModel
            {
                DoctorId = doctorId,
                HospitalId = hospitalId,
                LookupType = "INVALID_TYPE"
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Lookup type not found."));
        }

        [Test]
        public async Task Handle_NewPersonalizedData_ReturnsSuccess()
        {
            // Arrange
            var doctorId = Guid.NewGuid();
            var hospitalId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var lookupTypeId = 1;
            var lookupTypeCode = "TEST_TYPE";

            _context.Users.Add(TestEntityFactory.CreateUser(userId));
            _context.Doctors.Add(TestEntityFactory.CreateDoctor(doctorId, userId));
            _context.Hospitals.Add(TestEntityFactory.CreateHospital(hospitalId, userId));
            _context.LookupTypes.Add(new LookupType { LookupTypeId = lookupTypeId, LookupTypeCode = lookupTypeCode });
            _context.LookupMasters.Add(new LookupMaster { LookupTypeId = lookupTypeId, LookupId = Guid.NewGuid() });
            await _context.SaveChangesAsync();

            _mockDoctorValidationHelper.Setup(x => x.ValidateDoctorAsync(hospitalId, doctorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var request = new UpsertPersonalizedDataRequestModel
            {
                DoctorId = doctorId,
                HospitalId = hospitalId,
                LookupType = lookupTypeCode,
                Data = new PersonalizedLookupDataModel
                {
                    Code = "TEST",
                    Name = "Test Data",
                    ShortDesc = "Short Description"
                },
                LoggedInUserId = Guid.NewGuid()
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.Message, Is.EqualTo("Personalized data added"));
            Assert.That(response.PersonalId, Is.Not.Null);
            
            var storedData = await _context.LookupPersonals.FirstOrDefaultAsync(x => x.PersonalId == response.PersonalId);
            Assert.That(storedData, Is.Not.Null);
            Assert.That(storedData!.Name, Is.EqualTo("Test Data"));
            Assert.That(storedData.Code, Is.EqualTo("TEST"));
        }

         [Test]
        public async Task Handle_UpdateExistingPersonalizedData_ReturnsSuccess()
        {
            // Arrange
            var doctorId = Guid.NewGuid();
            var hospitalId = Guid.NewGuid();
            var lookupTypeId = 1;
            var lookupTypeCode = "TEST_TYPE";
            var personalId = Guid.NewGuid();

            var userId = Guid.NewGuid();
            _context.Users.Add(TestEntityFactory.CreateUser(userId));
            _context.Doctors.Add(TestEntityFactory.CreateDoctor(doctorId, userId));
            _context.Hospitals.Add(TestEntityFactory.CreateHospital(hospitalId, userId));
            _context.LookupTypes.Add(new LookupType { LookupTypeId = lookupTypeId, LookupTypeCode = lookupTypeCode });
            
            var existingData = new LookupPersonal
            {
                PersonalId = personalId,
                DoctorID = doctorId,
                HospitalID = hospitalId,
                LookupTypeId = lookupTypeId,
                Name = "Old Name"
            };
            _context.LookupPersonals.Add(existingData);
            await _context.SaveChangesAsync();

            _mockDoctorValidationHelper.Setup(x => x.ValidateDoctorAsync(hospitalId, doctorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var request = new UpsertPersonalizedDataRequestModel
            {
                DoctorId = doctorId,
                HospitalId = hospitalId,
                LookupType = lookupTypeCode,
                Data = new PersonalizedLookupDataModel
                {
                    PersonalId = personalId.ToString(),
                    Name = "New Name"
                },
                LoggedInUserId = Guid.NewGuid()
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.Message, Is.EqualTo("Personalized data updated"));
            
            var updatedData = await _context.LookupPersonals.FirstOrDefaultAsync(x => x.PersonalId == personalId);
            Assert.That(updatedData!.Name, Is.EqualTo("New Name"));
        }

        [Test]
        public async Task Handle_SourcePrescription_UsageCountIncrement_ReturnsSuccess()
        {
             // Arrange
            var doctorId = Guid.NewGuid();
            var hospitalId = Guid.NewGuid();
            var lookupTypeId = 1;
            var lookupTypeCode = "TEST_TYPE";
            var personalId = Guid.NewGuid();

            var userId = Guid.NewGuid();
            _context.Users.Add(TestEntityFactory.CreateUser(userId));
            _context.Doctors.Add(TestEntityFactory.CreateDoctor(doctorId, userId));
            _context.Hospitals.Add(TestEntityFactory.CreateHospital(hospitalId, userId));
            _context.LookupTypes.Add(new LookupType { LookupTypeId = lookupTypeId, LookupTypeCode = lookupTypeCode });
            
            var existingData = new LookupPersonal
            {
                PersonalId = personalId,
                DoctorID = doctorId,
                HospitalID = hospitalId,
                LookupTypeId = lookupTypeId,
                Name = "Paracetamol",
                UsageCount = 5
            };
            _context.LookupPersonals.Add(existingData);
            await _context.SaveChangesAsync();

            _mockDoctorValidationHelper.Setup(x => x.ValidateDoctorAsync(hospitalId, doctorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var request = new UpsertPersonalizedDataRequestModel
            {
                DoctorId = doctorId,
                HospitalId = hospitalId,
                LookupType = lookupTypeCode,
                Source = "prescription",
                Data = new PersonalizedLookupDataModel
                {
                    Name = "Paracetamol"
                },
                LoggedInUserId = Guid.NewGuid()
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.Message, Is.EqualTo("Personalized data usage count updated"));
            
            var updatedData = await _context.LookupPersonals.FirstOrDefaultAsync(x => x.PersonalId == personalId);
            Assert.That(updatedData!.UsageCount, Is.EqualTo(6));
        }
    }
}
