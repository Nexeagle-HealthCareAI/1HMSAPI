using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.Helpers.Interfaces;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class GetPrescriptionAttachmentsHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<IDoctorValidationHelper> _doctorValidationHelperMock = null!;
        private GetPrescriptionAttachmentsHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
             _context = InMemoryDbContextFactory.CreateContext();
            _doctorValidationHelperMock = new Mock<IDoctorValidationHelper>();
            _handler = new GetPrescriptionAttachmentsHandler(_context, _doctorValidationHelperMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ReturnsAttachments()
        {
            // Arrange
            var hospitalId = Guid.NewGuid();
            var hospital = new Hospital { HospitalID = hospitalId, Name = "Hosp", Type = "General", RegistrationNumber = "REG001", Contact = "1234567890", Location = "Test Location", City = "Test City", State = "Test State", Country = "Test Country", Pincode = "123456", CreatedByUserID = Guid.NewGuid()  };
            _context.Hospitals.Add(hospital);

            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);

            var apptId = Guid.NewGuid();
            var patientId = "PAT1";

            var attachment = new PrescriptionAttachment
            {
                AttachmentId = Guid.NewGuid(),
                ApptId = apptId,
                PatientId = patientId,
                HospitalId = hospitalId,
                DoctorId = doctor.DoctorID,
                FileName = "File1.jpg"
            };
            _context.PrescriptionAttachments.Add(attachment);
            await _context.SaveChangesAsync();

            _doctorValidationHelperMock.Setup(x => x.ValidateDoctorAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var request = new GetPrescriptionAttachmentsRequestModel
            {
                 AppointmentId = apptId,
                 PatientId = patientId,
                 HospitalId = hospitalId,
                 DoctorId = doctor.DoctorID
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.Attachments, Has.Count.EqualTo(1));
            Assert.That(response.Attachments[0].FileName, Is.EqualTo("File1.jpg"));
        }
    }
}
