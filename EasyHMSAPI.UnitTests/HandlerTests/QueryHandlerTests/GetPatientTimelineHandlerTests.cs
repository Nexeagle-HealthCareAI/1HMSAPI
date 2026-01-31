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
    public class GetPatientTimelineHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<IDoctorValidationHelper> _doctorValidationHelperMock = null!;
        private GetPatientTimelineHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _doctorValidationHelperMock = new Mock<IDoctorValidationHelper>();
            _handler = new GetPatientTimelineHandler(_context, _doctorValidationHelperMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ReturnsTimeline()
        {
            // Arrange
            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            var hospitalId = Guid.NewGuid();
            var hospital = new Hospital { HospitalID = hospitalId, Name = "Hosp", Type = "General", RegistrationNumber = "REG001", Contact = "1234567890", Location = "Test Location", City = "Test City", State = "Test State", Country = "Test Country", Pincode = "123456", CreatedByUserID = Guid.NewGuid()  };
            _context.Hospitals.Add(hospital);

            var patientId = "PAT1";
            var appointment = new Appointment
            {
                ApptId = Guid.NewGuid(),
                DoctorId = doctor.DoctorID,
                HospitalId = hospitalId,
                PatientId = patientId,
                ApptDate = DateTime.Today,
                CurrentStatusCode = "Completed"
            };
            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            _doctorValidationHelperMock.Setup(x => x.ValidateDoctorAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var request = new GetPatientTimelineRequestModel
            {
                HospitalId = hospitalId,
                DoctorId = doctor.DoctorID,
                PatientId = patientId
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.Data[0].TimelineData, Has.Count.EqualTo(1));
            Assert.That(response.Data[0].TimelineData[0].Status, Is.EqualTo("Completed"));
        }

        [Test]
        public async Task Handle_PatientTimeline_NoAppointments()
        {
           // Arrange
           var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            var hospitalId = Guid.NewGuid();
            var hospital = new Hospital { HospitalID = hospitalId, Name = "Hosp", Type = "General", RegistrationNumber = "REG001", Contact = "1234567890", Location = "Test Location", City = "Test City", State = "Test State", Country = "Test Country", Pincode = "123456", CreatedByUserID = Guid.NewGuid()  };
            _context.Hospitals.Add(hospital);
            await _context.SaveChangesAsync();
            
             _doctorValidationHelperMock.Setup(x => x.ValidateDoctorAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
                
             var request = new GetPatientTimelineRequestModel
            {
                HospitalId = hospitalId,
                DoctorId = doctor.DoctorID,
                PatientId = "PAT1"
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.Message, Does.Contain("No appointments found"));
        }
    }
}
