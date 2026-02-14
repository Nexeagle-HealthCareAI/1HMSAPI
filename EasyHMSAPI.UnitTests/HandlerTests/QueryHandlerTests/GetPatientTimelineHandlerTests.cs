using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.Application.Helpers.Interfaces;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class GetPatientTimelineHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<IDoctorValidationHelper> _mockDoctorValidationHelper = null!;
        private GetPatientTimelineHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _mockDoctorValidationHelper = new Mock<IDoctorValidationHelper>();
            _handler = new GetPatientTimelineHandler(_context, _mockDoctorValidationHelper.Object);
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
            var request = new GetPatientTimelineRequestModel
            {
                DoctorId = Guid.NewGuid(),
                HospitalId = Guid.NewGuid(),
                PatientId = "PAT123"
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

            var request = new GetPatientTimelineRequestModel
            {
                DoctorId = doctorId,
                HospitalId = Guid.NewGuid(),
                PatientId = "PAT123"
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

            var request = new GetPatientTimelineRequestModel
            {
                DoctorId = doctorId,
                HospitalId = hospitalId,
                PatientId = "PAT123"
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Doctor is not associated with the specified hospital."));
        }

        [Test]
        public async Task Handle_NoAppointmentsOnly_ReturnsEmptySuccess()
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

            var request = new GetPatientTimelineRequestModel
            {
                DoctorId = doctorId,
                HospitalId = hospitalId,
                PatientId = "PAT123"
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.Message, Is.EqualTo("No appointments found for the patient."));
        }

        [Test]
        public async Task Handle_Success_ReturnsTimelineData()
        {
            // Arrange
            var doctorId = Guid.NewGuid();
            var hospitalId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var patientId = "PAT123";
            var appointmentId = Guid.NewGuid();
            var prescriptionId = Guid.NewGuid();
            var attachmentId = Guid.NewGuid();

            _context.Users.Add(TestEntityFactory.CreateUser(userId));
            _context.Doctors.Add(TestEntityFactory.CreateDoctor(doctorId, userId));
            _context.Hospitals.Add(TestEntityFactory.CreateHospital(hospitalId, userId));
            _context.Appointments.Add(TestEntityFactory.CreateAppointment(appointmentId, hospitalId, doctorId, patientId));
            
            var prescription = TestEntityFactory.CreatePrescription(prescriptionId, appointmentId, doctorId, hospitalId, patientId);
            prescription.ChiefComplaint = "Headache";
            _context.Prescription.Add(prescription);

            _context.PrescriptionAttachments.Add(TestEntityFactory.CreatePrescriptionAttachment(attachmentId, appointmentId, doctorId, hospitalId, patientId));
            
            await _context.SaveChangesAsync();

            _mockDoctorValidationHelper.Setup(x => x.ValidateDoctorAsync(hospitalId, doctorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var request = new GetPatientTimelineRequestModel
            {
                DoctorId = doctorId,
                HospitalId = hospitalId,
                PatientId = patientId
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.Message, Is.EqualTo("Patient timeline retrieved successfully."));
            Assert.That(response.Data, Is.Not.Null);
            Assert.That(response.Data![0].TimelineData.Count, Is.EqualTo(1));
            
            var timelineItem = response.Data[0].TimelineData[0];
            Assert.That(timelineItem.ApptID, Is.EqualTo(appointmentId));
            Assert.That(timelineItem.ChiefComplaint, Is.EqualTo("Headache"));
            Assert.That(timelineItem.Attachments, Is.Not.Null);
            Assert.That(timelineItem.Attachments![0].FileName, Is.EqualTo("report.pdf"));
        }
    }
}
