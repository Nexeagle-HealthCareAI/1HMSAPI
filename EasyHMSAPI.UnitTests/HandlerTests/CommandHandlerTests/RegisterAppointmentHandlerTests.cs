using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class RegisterAppointmentHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<ISmsService> _smsServiceMock = null!;
        private Mock<IWhatsAppMessagingService> _whatsAppServiceMock = null!;
        private RegisterAppointmentHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _smsServiceMock = new Mock<ISmsService>();
            _whatsAppServiceMock = new Mock<IWhatsAppMessagingService>();
            
            _handler = new RegisterAppointmentHandler(_context, _smsServiceMock.Object, _whatsAppServiceMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_NewPatient_CreatesAppointment()
        {
             // Arrange
            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            var hospitalId = Guid.NewGuid();
            var hospital = new Hospital { HospitalID = hospitalId, Name = "Hosp", Email = "e@m.com", Type = "General", RegistrationNumber = "REG001", Contact = "1234567890", Location = "Test Location", City = "Test City", State = "Test State", Country = "Test Country", Pincode = "123456", CreatedByUserID = Guid.NewGuid()  };
            _context.Hospitals.Add(hospital);
            await _context.SaveChangesAsync();
            
            var request = new RegisterAppointmentRequestModel
            {
                UserId = user.UserID,
                DoctorId = doctor.DoctorID,
                HospitalId = hospitalId,
                ApptDate = DateTime.Today.AddDays(1),
                StartAt = DateTime.Today.AddDays(1).AddHours(10),
                Patient = new Patient
                {
                    FullName = "New Patient",
                    Mobile = "9876543210",
                    AgeYears = 30,
                    Sex = "Male"
                },
                AllocateToken = true
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Status, Is.Not.Null);
            Assert.That(response.AppointmentId, Is.Not.EqualTo(Guid.Empty));
            Assert.That(response.TokenNumber, Is.Not.Null);

            var patient = await _context.PatientRegistrations.FirstOrDefaultAsync(p => p.Mobile == "9876543210");
            Assert.That(patient, Is.Not.Null);
            Assert.That(patient!.FullName, Is.EqualTo("New Patient"));

            var appointment = await _context.Appointments.FindAsync(response.AppointmentId);
            Assert.That(appointment, Is.Not.Null);
            Assert.That(appointment!.DoctorId, Is.EqualTo(doctor.DoctorID));
        }

        [Test]
        public async Task Handle_DoctorNotActive_ThrowsException()
        {
            // Arrange
            var request = new RegisterAppointmentRequestModel { DoctorId = Guid.NewGuid() }; // Non-existent doctor

            // Act & Assert
            var ex = Assert.ThrowsAsync<Exception>(() => _handler.Handle(request, CancellationToken.None));
            Assert.That(ex.Message, Does.Contain("Failed to register appointment"));
        }
    }
}
