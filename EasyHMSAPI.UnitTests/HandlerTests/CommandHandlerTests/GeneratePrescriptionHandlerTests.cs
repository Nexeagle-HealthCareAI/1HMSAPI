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
    public class GeneratePrescriptionHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<IDoctorValidationHelper> _doctorValidationHelperMock = null!;
        private GeneratePrescriptionHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _doctorValidationHelperMock = new Mock<IDoctorValidationHelper>();
            _handler = new GeneratePrescriptionHandler(_context, _doctorValidationHelperMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ValidRequest_ReturnsPrescriptionData()
        {
            // Arrange
            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            var hospitalId = Guid.NewGuid();
            var hospital = new Hospital { HospitalID = hospitalId, Name = "Test Hospital", Email = "a@b.com", Type = "General", RegistrationNumber = "REG001", Contact = "1234567890", Location = "Test Location", City = "Test City", State = "Test State", Country = "Test Country", Pincode = "123456", CreatedByUserID = Guid.NewGuid()  };
            _context.Hospitals.Add(hospital);

            var patient = new PatientRegistration 
            { 
               PatientId = "PAT123", 
               HospitalId = hospitalId,
               FullName = "Test Patient",
               Mobile = "1234567890"
            };
            _context.PatientRegistrations.Add(patient);

            var appointment = new Appointment
            {
                ApptId = Guid.NewGuid(),
                DoctorId = doctor.DoctorID,
                HospitalId = hospitalId,
                PatientId = "PAT123",
                CurrentStatusCode = "Booked",
                ValidUptoDate = DateTime.Today.AddDays(7)
            };
            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            _doctorValidationHelperMock.Setup(x => x.ValidateDoctorAsync(hospitalId, doctor.DoctorID, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var request = new GeneratePrescriptionRequestModel
            {
                AppointmentId = appointment.ApptId,
                DoctorId = doctor.DoctorID,
                HospitalId = hospitalId,
                PatientId = patient.PatientId
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.Data, Is.Not.Null);
            Assert.That(response.Data?.PatientData?.PatientDetails, Is.Not.Null);
            Assert.That(response.Data?.PatientData?.PatientDetails?[0].Name, Is.EqualTo(patient.FullName));
        }

         [Test]
        public async Task Handle_InvalidAppointment_ReturnsFailure()
        {
            // Arrange
            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            var hospitalId = Guid.NewGuid();
            var hospital = new Hospital { HospitalID = hospitalId, Name = "Test Hospital", Email = "a@b.com", Type = "General", RegistrationNumber = "REG001", Contact = "1234567890", Location = "Test Location", City = "Test City", State = "Test State", Country = "Test Country", Pincode = "123456", CreatedByUserID = Guid.NewGuid()  };
            _context.Hospitals.Add(hospital);
             var patient = new PatientRegistration 
            { 
               PatientId = "PAT123", 
               HospitalId = hospitalId,
               FullName = "Test Patient",
               Mobile = "1234567890"
            };
            _context.PatientRegistrations.Add(patient);
             await _context.SaveChangesAsync();

            _doctorValidationHelperMock.Setup(x => x.ValidateDoctorAsync(hospitalId, doctor.DoctorID, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var request = new GeneratePrescriptionRequestModel
            {
                AppointmentId = Guid.NewGuid(), // Non-existent appointment
                DoctorId = doctor.DoctorID,
                HospitalId = hospitalId,
                PatientId = patient.PatientId
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("Invalid appointment Id"));
        }
    }
}
