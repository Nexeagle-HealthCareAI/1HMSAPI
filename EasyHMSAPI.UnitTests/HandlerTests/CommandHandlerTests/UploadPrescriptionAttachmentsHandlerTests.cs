using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.Helpers.Interfaces;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class UploadPrescriptionAttachmentsHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<IDoctorValidationHelper> _doctorValidationHelperMock = null!;
        private Mock<IBlobStorageService> _blobStorageServiceMock = null!;
        private Mock<IConfiguration> _configurationMock = null!;
        private UploadPrescriptionAttachmentsHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _doctorValidationHelperMock = new Mock<IDoctorValidationHelper>();
            _blobStorageServiceMock = new Mock<IBlobStorageService>();
            _configurationMock = new Mock<IConfiguration>();
            
            _configurationMock.SetupGet(x => x["BlobStorage:PrescriptionAttachmentsContainer"]).Returns("prescriptions");

            _handler = new UploadPrescriptionAttachmentsHandler(
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
        public async Task Handle_ValidRequest_UploadsAttachment()
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
                CurrentStatusCode = AppConstants.AppointmentStatus_LabRequired
            };
            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            _doctorValidationHelperMock.Setup(x => x.ValidateDoctorAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            
            var fileMock = new Mock<IFormFile>();
            _blobStorageServiceMock.Setup(x => x.UploadAsync(It.IsAny<string>(), It.IsAny<IFormFile>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("file.pdf|http://url.com/file.pdf");

            var request = new UploadPrescriptionAttachmentsRequestModel
            {
                AppointmentId = appointment.ApptId,
                DoctorId = doctor.DoctorID,
                HospitalId = hospitalId,
                PatientId = "PAT123",
                File = fileMock.Object
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.FileUrl, Is.EqualTo("http://url.com/file.pdf"));
            
            var updatedAppt = await _context.Appointments.FindAsync(appointment.ApptId);
            Assert.That(updatedAppt!.CurrentStatusCode, Is.EqualTo(AppConstants.AppointmentStatus_AwaitingReconsult));
        }

        [Test]
        public async Task Handle_InvalidStatus_ReturnsMessage()
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
                CurrentStatusCode = "Booked" // Not allowed status
            };
            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            _doctorValidationHelperMock.Setup(x => x.ValidateDoctorAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var request = new UploadPrescriptionAttachmentsRequestModel
            {
                AppointmentId = appointment.ApptId,
                DoctorId = doctor.DoctorID,
                HospitalId = hospitalId,
                PatientId = "PAT123",
                File = new Mock<IFormFile>().Object
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("not allowed"));
        }
    }
}
