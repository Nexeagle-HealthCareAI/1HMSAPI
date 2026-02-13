using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Data.Constants;
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
    public class GeneratePrescriptionHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<IDoctorValidationHelper> _mockDoctorValidationHelper = null!;
        private GeneratePrescriptionHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _mockDoctorValidationHelper = new Mock<IDoctorValidationHelper>();
            _handler = new GeneratePrescriptionHandler(_context, _mockDoctorValidationHelper.Object);
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
            var request = new GeneratePrescriptionRequestModel
            {
                DoctorId = Guid.NewGuid(),
                HospitalId = Guid.NewGuid(),
                PatientId = "PAT123",
                AppointmentId = Guid.NewGuid()
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Invalid doctor Id"));
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

            var request = new GeneratePrescriptionRequestModel
            {
                DoctorId = doctorId,
                HospitalId = Guid.NewGuid(),
                PatientId = "PAT123",
                AppointmentId = Guid.NewGuid()
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Invalid hospital Id"));
        }

        [Test]
        public async Task Handle_ValidationFailed_ReturnsError()
        {
            // Arrange
            var doctorId = Guid.NewGuid();
            var hospitalId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            
            var user = TestEntityFactory.CreateUser(userId);
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            
            _context.Doctors.Add(TestEntityFactory.CreateDoctor(doctorId, userId));
            _context.Hospitals.Add(TestEntityFactory.CreateHospital(hospitalId, userId));
            await _context.SaveChangesAsync();
            
            _mockDoctorValidationHelper.Setup(x => x.ValidateDoctorAsync(hospitalId, doctorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var request = new GeneratePrescriptionRequestModel
            {
                DoctorId = doctorId,
                HospitalId = hospitalId,
                PatientId = "PAT123",
                AppointmentId = Guid.NewGuid()
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Doctor is not associated with the specified hospital."));
        }

        [Test]
        public async Task Handle_PatientNotFound_ReturnsError()
        {
            // Arrange
            var doctorId = Guid.NewGuid();
            var hospitalId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            
            var user = TestEntityFactory.CreateUser(userId);
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            
            _context.Doctors.Add(TestEntityFactory.CreateDoctor(doctorId, userId));
            _context.Hospitals.Add(TestEntityFactory.CreateHospital(hospitalId, userId));
            await _context.SaveChangesAsync();

            _mockDoctorValidationHelper.Setup(x => x.ValidateDoctorAsync(hospitalId, doctorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var request = new GeneratePrescriptionRequestModel
            {
                DoctorId = doctorId,
                HospitalId = hospitalId,
                PatientId = "INVALID_PATIENT",
                AppointmentId = Guid.NewGuid()
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Invalid patient Id"));
        }

        [Test]
        public async Task Handle_AppointmentNotFound_ReturnsError()
        {
             // Arrange
            var doctorId = Guid.NewGuid();
            var hospitalId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var patientId = "PAT123";
            
            var user = TestEntityFactory.CreateUser(userId);
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            
            _context.Doctors.Add(TestEntityFactory.CreateDoctor(doctorId, userId));
            _context.Hospitals.Add(TestEntityFactory.CreateHospital(hospitalId, userId));
            _context.PatientRegistrations.Add(TestEntityFactory.CreatePatientRegistration(hospitalId, patientId));
            await _context.SaveChangesAsync();

            _mockDoctorValidationHelper.Setup(x => x.ValidateDoctorAsync(hospitalId, doctorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var request = new GeneratePrescriptionRequestModel
            {
                DoctorId = doctorId,
                HospitalId = hospitalId,
                PatientId = patientId,
                AppointmentId = Guid.NewGuid()
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("Invalid appointment Id"));
        }

        [Test]
        public async Task Handle_Success_ReturnsPrescriptionData()
        {
            // Arrange
            var doctorId = Guid.NewGuid();
            var hospitalId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var patientId = "PAT123";
            var appointmentId = Guid.NewGuid();
            var prescriptionId = Guid.NewGuid();

            var user = TestEntityFactory.CreateUser(userId);
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            
            _context.Doctors.Add(TestEntityFactory.CreateDoctor(doctorId, userId));
            _context.Hospitals.Add(TestEntityFactory.CreateHospital(hospitalId, userId));
            _context.PatientRegistrations.Add(TestEntityFactory.CreatePatientRegistration(hospitalId, patientId, "John Doe"));
            _context.Appointments.Add(TestEntityFactory.CreateAppointment(appointmentId, hospitalId, doctorId, patientId));
            _context.PrescriptionSettings.Add(TestEntityFactory.CreatePrescriptionSetting(hospitalId, doctorId));
            
            var prescription = TestEntityFactory.CreatePrescription(prescriptionId, appointmentId, doctorId, hospitalId, patientId);
            prescription.ChiefComplaint = "Fever";
            _context.Prescription.Add(prescription);

            _context.PrescriptionMedicine.Add(new PrescriptionMedicine
            {
                PresMedicineId = Guid.NewGuid(),
                PrescriptionId = prescriptionId,
                MedicineName = "Paracetamol",
                Dosage = "500mg",
                Frequency = "BID",
                Durations = "3 Days",
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            _mockDoctorValidationHelper.Setup(x => x.ValidateDoctorAsync(hospitalId, doctorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var request = new GeneratePrescriptionRequestModel
            {
                DoctorId = doctorId,
                HospitalId = hospitalId,
                PatientId = patientId,
                AppointmentId = appointmentId
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.Message, Is.EqualTo("Prescription details generated successfully."));
            Assert.That(response.Data, Is.Not.Null);
            Assert.That(response.Data!.PatientData.PatientDetails[0].Name, Is.EqualTo("John Doe"));
            Assert.That(response.Data.ChiefComplaint, Is.EqualTo("Fever"));
            Assert.That(response.Data.Medications, Is.Not.Null);
            Assert.That(response.Data.Medications!.Count, Is.EqualTo(1));
            Assert.That(response.Data.Medications[0].DrugName, Is.EqualTo("Paracetamol"));
            Assert.That(response.Data.Template, Is.Not.Null);
            Assert.That(response.Data.Template!.FontFamily, Is.EqualTo("Arial"));
        }
    }
}
