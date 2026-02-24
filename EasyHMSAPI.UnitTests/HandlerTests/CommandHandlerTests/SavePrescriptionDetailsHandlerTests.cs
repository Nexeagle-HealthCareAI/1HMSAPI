using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.Application.Helpers.Interfaces;
using EasyHMSAPI.Application.Services.Interfaces;
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
        private Mock<IDoctorValidationHelper> _mockDoctorValidationHelper = null!;
        private Mock<IBlobStorageService> _mockBlobStorageService = null!;
        private Mock<IConfiguration> _mockConfiguration = null!;
        private SavePrescriptionDetailsHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _mockDoctorValidationHelper = new Mock<IDoctorValidationHelper>();
            _mockBlobStorageService = new Mock<IBlobStorageService>();
            _mockConfiguration = new Mock<IConfiguration>();
            _mockConfiguration.Setup(x => x["BlobStorage:PrescriptionsContainer"]).Returns("prescriptions");

            _handler = new SavePrescriptionDetailsHandler(_context, _mockDoctorValidationHelper.Object, _mockBlobStorageService.Object, _mockConfiguration.Object);
        }

        [TearDown]
        public void TearDown()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        private async Task SetupEntities(Guid doctorId, Guid hospitalId, Guid userId, Guid appointmentId)
        {
            _context.Users.Add(new User 
            { 
                UserID = userId, 
                MobileNumber = "1234567890", 
                UserStatusId = 1 
            });
            
            _context.Hospitals.Add(new Hospital 
            { 
                HospitalID = hospitalId, 
                Name = "Test Hospital", 
                CreatedByUserID = userId,
                Type = "General",
                RegistrationNumber = "REG123",
                Contact = "9999999999",
                Location = "Test Location",
                City = "Test City",
                State = "Test State",
                Country = "Test Country",
                Pincode = "123456",
                CreatedByUser = new User { UserID = Guid.NewGuid(), MobileNumber = "0000000000" } // Dummy
            });

            _context.Doctors.Add(new Doctor 
            { 
                DoctorID = doctorId, 
                UserID = userId, 
                LicenseNumber = "DOC123" 
            });

            _context.Appointments.Add(new Appointment 
            { 
                ApptId = appointmentId, 
                DoctorId = doctorId, 
                HospitalId = hospitalId,
                CurrentStatusCode = "scheduled" 
            });

            await _context.SaveChangesAsync();
        }

        [Test]
        public async Task Handle_DoctorNotFound_ReturnsError()
        {
            // Arrange
            var request = new SavePrescriptionDetailsRequestModel
            {
                DoctorId = Guid.NewGuid(),
                HospitalId = Guid.NewGuid(),
                AppointmentId = Guid.NewGuid()
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
            _context.Doctors.Add(new Doctor { DoctorID = doctorId, LicenseNumber = "123" });
            await _context.SaveChangesAsync();

            var request = new SavePrescriptionDetailsRequestModel
            {
                DoctorId = doctorId,
                HospitalId = Guid.NewGuid(),
                AppointmentId = Guid.NewGuid()
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
            
            await SetupEntities(doctorId, hospitalId, userId, Guid.NewGuid());
            
            _mockDoctorValidationHelper.Setup(x => x.ValidateDoctorAsync(hospitalId, doctorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var request = new SavePrescriptionDetailsRequestModel
            {
                DoctorId = doctorId,
                HospitalId = hospitalId,
                AppointmentId = Guid.NewGuid()
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Doctor is not associated with the specified hospital."));
        }

        [Test]
        public async Task Handle_NewPrescription_Success()
        {
            // Arrange
            var doctorId = Guid.NewGuid();
            var hospitalId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var appointmentId = Guid.NewGuid();

            await SetupEntities(doctorId, hospitalId, userId, appointmentId);

            _mockDoctorValidationHelper.Setup(x => x.ValidateDoctorAsync(hospitalId, doctorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var request = new SavePrescriptionDetailsRequestModel
            {
                DoctorId = doctorId,
                HospitalId = hospitalId,
                AppointmentId = appointmentId,
                PatientId = "PAT123",
                ChiefComplaint = "Headache",
                LoggedInUserId = userId,
                CurrentDateTime = DateTime.UtcNow
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.Message, Is.EqualTo("Prescription saved."));

            var prescription = await _context.Prescription.FirstOrDefaultAsync(p => p.ApptId == appointmentId);
            Assert.That(prescription, Is.Not.Null);
            Assert.That(prescription!.ChiefComplaint, Is.EqualTo("Headache"));
        }

        [Test]
        public async Task Handle_UpdatePrescription_Success()
        {
            // Arrange
            var doctorId = Guid.NewGuid();
            var hospitalId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var appointmentId = Guid.NewGuid();
            var prescriptionId = Guid.NewGuid();

            await SetupEntities(doctorId, hospitalId, userId, appointmentId);
            
            _context.Prescription.Add(new Prescription
            {
                PrescriptionId = prescriptionId,
                ApptId = appointmentId,
                DoctorId = doctorId,
                HospitalId = hospitalId,
                ChiefComplaint = "Old Complaint",
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            _mockDoctorValidationHelper.Setup(x => x.ValidateDoctorAsync(hospitalId, doctorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var request = new SavePrescriptionDetailsRequestModel
            {
                PrescriptionId = prescriptionId,
                DoctorId = doctorId,
                HospitalId = hospitalId,
                AppointmentId = appointmentId,
                ChiefComplaint = "New Complaint",
                LoggedInUserId = userId,
                CurrentDateTime = DateTime.UtcNow
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.Message, Is.EqualTo("Prescription saved for later."));

            var prescription = await _context.Prescription.FindAsync(prescriptionId);
            Assert.That(prescription!.ChiefComplaint, Is.EqualTo("New Complaint"));
        }

        [Test]
        public async Task Handle_PrescriptionWithMedications_Success()
        {
             // Arrange
            var doctorId = Guid.NewGuid();
            var hospitalId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var appointmentId = Guid.NewGuid();

            await SetupEntities(doctorId, hospitalId, userId, appointmentId);

            _mockDoctorValidationHelper.Setup(x => x.ValidateDoctorAsync(hospitalId, doctorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var meds = new List<MedicationModel>
            {
                new MedicationModel 
                { 
                    DrugName = "Paracetamol", 
                    Dose = "500mg", 
                    Frequency = "BID", 
                    Duration = "3 Days" 
                }
            };

            var request = new SavePrescriptionDetailsRequestModel
            {
                DoctorId = doctorId,
                HospitalId = hospitalId,
                AppointmentId = appointmentId,
                Medications = meds,
                LoggedInUserId = userId,
                CurrentDateTime = DateTime.UtcNow
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            
            var prescription = await _context.Prescription.FirstOrDefaultAsync(p => p.ApptId == appointmentId);
            Assert.That(prescription, Is.Not.Null);

            var savedMeds = await _context.PrescriptionMedicine.Where(m => m.PrescriptionId == prescription!.PrescriptionId).ToListAsync();
            Assert.That(savedMeds.Count, Is.EqualTo(1));
            Assert.That(savedMeds[0].MedicineName, Is.EqualTo("Paracetamol"));
        }

        [Test]
        public async Task Handle_SubmitAction_UpdatesStatus()
        {
            // Arrange
            var doctorId = Guid.NewGuid();
            var hospitalId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var appointmentId = Guid.NewGuid();

            await SetupEntities(doctorId, hospitalId, userId, appointmentId);

            _mockDoctorValidationHelper.Setup(x => x.ValidateDoctorAsync(hospitalId, doctorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var request = new SavePrescriptionDetailsRequestModel
            {
                DoctorId = doctorId,
                HospitalId = hospitalId,
                AppointmentId = appointmentId,
                ActionType = AppConstants.Prescription_ActionType_Submit,
                LoggedInUserId = userId,
                CurrentDateTime = DateTime.UtcNow
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);

            var prescription = await _context.Prescription.FirstOrDefaultAsync(p => p.ApptId == appointmentId);
            Assert.That(prescription!.Status, Is.EqualTo(AppConstants.AppointmentStatus_Completed));

            var appointment = await _context.Appointments.FindAsync(appointmentId);
            Assert.That(appointment!.CurrentStatusCode, Is.EqualTo(AppConstants.AppointmentStatus_Completed));
        }
    }
}
