using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.Helpers.Interfaces;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class SavePrescriptionDetailsHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<IDoctorValidationHelper> _doctorValidationHelperMock = null!;
        private Mock<IBlobStorageService> _blobStorageServiceMock = null!;
        private Mock<IConfiguration> _configurationMock = null!;
        private SavePrescriptionDetailsHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _doctorValidationHelperMock = new Mock<IDoctorValidationHelper>();
            _blobStorageServiceMock = new Mock<IBlobStorageService>();
            _configurationMock = new Mock<IConfiguration>();
            
            _configurationMock.SetupGet(x => x["BlobStorage:PrescriptionsContainer"]).Returns("prescriptions");

            _handler = new SavePrescriptionDetailsHandler(
                _context, 
                _doctorValidationHelperMock.Object, 
                _blobStorageServiceMock.Object, 
                _configurationMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ValidRequest_SavesPrescription()
        {
             // Arrange
            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            var hospitalId = Guid.NewGuid();
            var hospital = new Hospital { HospitalID = hospitalId, Name = "Hosp", Email = "e@m.com", Type = "General", RegistrationNumber = "REG001", Contact = "1234567890", Location = "Test Location", City = "Test City", State = "Test State", Country = "Test Country", Pincode = "123456", CreatedByUserID = Guid.NewGuid()  };
            _context.Hospitals.Add(hospital);

            var appointment = new Appointment
            {
                ApptId = Guid.NewGuid(),
                DoctorId = doctor.DoctorID,
                HospitalId = hospitalId,
                PatientId = "PAT123",
                CurrentStatusCode = "Booked"
            };
            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            _doctorValidationHelperMock.Setup(x => x.ValidateDoctorAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var request = new SavePrescriptionDetailsRequestModel
            {
                AppointmentId = appointment.ApptId,
                DoctorId = doctor.DoctorID,
                HospitalId = hospitalId,
                PatientId = "PAT123",
                CurrentDateTime = DateTime.UtcNow,
                ChiefComplaint = "Headache",
                Diagnosis = "Migraine"
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.Message, Is.EqualTo("Prescription saved."));
            
            var prescription = await _context.Prescription.FirstOrDefaultAsync(p => p.ApptId == appointment.ApptId);
            Assert.That(prescription, Is.Not.Null);
            Assert.That(prescription!.ChiefComplaint, Is.EqualTo("Headache"));
        }

        [Test]
        public async Task Handle_AppointmentNotFound_ReturnsFailure()
        {
            // Arrange
             var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
             var hospitalId = Guid.NewGuid();
            var hospital = new Hospital { HospitalID = hospitalId, Name = "Hosp", Email = "e@m.com", Type = "General", RegistrationNumber = "REG001", Contact = "1234567890", Location = "Test Location", City = "Test City", State = "Test State", Country = "Test Country", Pincode = "123456", CreatedByUserID = Guid.NewGuid()  };
            _context.Hospitals.Add(hospital);
            await _context.SaveChangesAsync();

            _doctorValidationHelperMock.Setup(x => x.ValidateDoctorAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var request = new SavePrescriptionDetailsRequestModel 
            { 
                 AppointmentId = Guid.NewGuid(),
                 DoctorId = doctor.DoctorID,
                 HospitalId = hospitalId
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Appointment not found."));
        }
    }
}
