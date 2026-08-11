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
        private Mock<IWhatsAppMessagingService> _whatsAppMessagingServiceMock = null!;
        private Mock<IConfiguration> _configurationMock = null!;
        private UploadPrescriptionAttachmentsHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _doctorValidationHelperMock = new Mock<IDoctorValidationHelper>();
            _blobStorageServiceMock = new Mock<IBlobStorageService>();
            _whatsAppMessagingServiceMock = new Mock<IWhatsAppMessagingService>();
            _configurationMock = new Mock<IConfiguration>();

            _configurationMock.SetupGet(x => x["BlobStorage:PrescriptionAttachmentsContainer"]).Returns("prescriptions");

            _handler = new UploadPrescriptionAttachmentsHandler(
                _context,
                _doctorValidationHelperMock.Object,
                _blobStorageServiceMock.Object,
                _whatsAppMessagingServiceMock.Object,
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

        [Test]
        public async Task Handle_ReportTypePrescription_PatientHasMobile_SendsWhatsApp()
        {
            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            var hospitalId = Guid.NewGuid();
            _context.Hospitals.Add(new Hospital { HospitalID = hospitalId, Name = "Hosp", Email = "e@m.com", Type = "General", RegistrationNumber = "REG001", Contact = "1234567890", Location = "Test Location", City = "Test City", State = "Test State", Country = "Test Country", Pincode = "123456", CreatedByUserID = Guid.NewGuid() });
            _context.PatientRegistrations.Add(new PatientRegistration { RegistrationId = Guid.NewGuid(), HospitalId = hospitalId, PatientId = "PAT123", Mobile = "9876543210", RegisteredAt = DateTime.UtcNow });

            var appointment = new Appointment { ApptId = Guid.NewGuid(), DoctorId = doctor.DoctorID, HospitalId = hospitalId, PatientId = "PAT123", CurrentStatusCode = AppConstants.AppointmentStatus_Ready };
            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            _doctorValidationHelperMock.Setup(x => x.ValidateDoctorAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _blobStorageServiceMock.Setup(x => x.UploadAsync(It.IsAny<string>(), It.IsAny<IFormFile>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("rx.pdf|http://url.com/rx.pdf");
            _whatsAppMessagingServiceMock.Setup(x => x.SendPrescriptionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            var request = new UploadPrescriptionAttachmentsRequestModel
            {
                AppointmentId = appointment.ApptId,
                DoctorId = doctor.DoctorID,
                HospitalId = hospitalId,
                PatientId = "PAT123",
                ReportType = "Prescription",
                File = new Mock<IFormFile>().Object
            };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            _whatsAppMessagingServiceMock.Verify(x => x.SendPrescriptionAsync("9876543210", "http://url.com/rx.pdf", "rx.pdf", "Hosp", It.IsAny<string>()), Times.Once);
        }

        [Test]
        public async Task Handle_ReportTypeNotPrescription_DoesNotSendWhatsApp()
        {
            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            var hospitalId = Guid.NewGuid();
            _context.Hospitals.Add(new Hospital { HospitalID = hospitalId, Name = "Hosp", Email = "e@m.com", Type = "General", RegistrationNumber = "REG001", Contact = "1234567890", Location = "Test Location", City = "Test City", State = "Test State", Country = "Test Country", Pincode = "123456", CreatedByUserID = Guid.NewGuid() });
            _context.PatientRegistrations.Add(new PatientRegistration { RegistrationId = Guid.NewGuid(), HospitalId = hospitalId, PatientId = "PAT123", Mobile = "9876543210", RegisteredAt = DateTime.UtcNow });

            var appointment = new Appointment { ApptId = Guid.NewGuid(), DoctorId = doctor.DoctorID, HospitalId = hospitalId, PatientId = "PAT123", CurrentStatusCode = AppConstants.AppointmentStatus_Ready };
            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            _doctorValidationHelperMock.Setup(x => x.ValidateDoctorAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _blobStorageServiceMock.Setup(x => x.UploadAsync(It.IsAny<string>(), It.IsAny<IFormFile>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("labreport.pdf|http://url.com/labreport.pdf");

            var request = new UploadPrescriptionAttachmentsRequestModel
            {
                AppointmentId = appointment.ApptId,
                DoctorId = doctor.DoctorID,
                HospitalId = hospitalId,
                PatientId = "PAT123",
                ReportType = "Lab Report",
                File = new Mock<IFormFile>().Object
            };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            _whatsAppMessagingServiceMock.Verify(x => x.SendPrescriptionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public async Task Handle_ClientSuppliedAttachmentId_IsHonored()
        {
            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            var hospitalId = Guid.NewGuid();
            _context.Hospitals.Add(new Hospital { HospitalID = hospitalId, Name = "Hosp", Email = "e@m.com", Type = "General", RegistrationNumber = "REG001", Contact = "1234567890", Location = "Test Location", City = "Test City", State = "Test State", Country = "Test Country", Pincode = "123456", CreatedByUserID = Guid.NewGuid() });

            var appointment = new Appointment { ApptId = Guid.NewGuid(), DoctorId = doctor.DoctorID, HospitalId = hospitalId, PatientId = "PAT123", CurrentStatusCode = AppConstants.AppointmentStatus_Ready };
            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            _doctorValidationHelperMock.Setup(x => x.ValidateDoctorAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _blobStorageServiceMock.Setup(x => x.UploadAsync(It.IsAny<string>(), It.IsAny<IFormFile>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("rx.pdf|http://url.com/rx.pdf");

            var preGeneratedId = Guid.NewGuid();
            var request = new UploadPrescriptionAttachmentsRequestModel
            {
                AppointmentId = appointment.ApptId,
                DoctorId = doctor.DoctorID,
                HospitalId = hospitalId,
                PatientId = "PAT123",
                AttachmentId = preGeneratedId,
                File = new Mock<IFormFile>().Object
            };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.AttachmentId, Is.EqualTo(preGeneratedId));
        }
    }
}
