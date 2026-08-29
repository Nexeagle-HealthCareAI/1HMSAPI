using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
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
        private Mock<IMediator> _mediatorMock = null!;
        private RegisterAppointmentHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _smsServiceMock = new Mock<ISmsService>();
            _whatsAppServiceMock = new Mock<IWhatsAppMessagingService>();
            _mediatorMock = new Mock<IMediator>();

            _handler = new RegisterAppointmentHandler(_context, _smsServiceMock.Object, _whatsAppServiceMock.Object, _mediatorMock.Object, new MemoryCache(new MemoryCacheOptions()));
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
                    Age = 30,
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

        [Test]
        public async Task Handle_RescheduleWithoutExplicitSlot_MovesStartAtToTheNewDate()
        {
            // RescheduleDialog (the frontend's only reschedule entry point) sends a new ApptDate
            // but never a StartAt, relying on the auto-pick-first-available-slot fallback below.
            // With no DoctorShiftOverrides/DoctorShiftTemplates seeded, that fallback can't find a
            // slot at all -- it used to leave StartAt/EndAt on the OLD date entirely in that case.
            // Every appointment-listing view (Future Appointments, Doc Board Upcoming, the
            // availability calendar) buckets by StartAt, not ApptDate, so a "successfully"
            // rescheduled appointment would silently vanish from all of them.
            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            var hospitalId = Guid.NewGuid();
            var hospital = new Hospital { HospitalID = hospitalId, Name = "Hosp", Email = "e@m.com", Type = "General", RegistrationNumber = "REG002", Contact = "1234567890", Location = "Test Location", City = "Test City", State = "Test State", Country = "Test Country", Pincode = "123456", CreatedByUserID = Guid.NewGuid() };
            _context.Hospitals.Add(hospital);
            await _context.SaveChangesAsync();

            var originalDate = DateTime.Today.AddDays(1);
            var created = await _handler.Handle(new RegisterAppointmentRequestModel
            {
                UserId = user.UserID,
                DoctorId = doctor.DoctorID,
                HospitalId = hospitalId,
                ApptDate = originalDate,
                StartAt = originalDate.AddHours(10),
                Patient = new Patient { FullName = "Reschedule Patient", Mobile = "9876500000", Age = 40, Sex = "Female" },
                AllocateToken = true
            }, CancellationToken.None);

            var newDate = DateTime.Today.AddDays(5);
            await _handler.Handle(new RegisterAppointmentRequestModel
            {
                UserId = user.UserID,
                DoctorId = doctor.DoctorID,
                HospitalId = hospitalId,
                ApptDate = newDate,
                AppointmentId = created.AppointmentId,
                // No StartAt -- mirrors RescheduleDialog exactly.
            }, CancellationToken.None);

            var appointment = await _context.Appointments.FindAsync(created.AppointmentId);
            Assert.That(appointment, Is.Not.Null);
            Assert.That(appointment!.ApptDate.Date, Is.EqualTo(newDate.Date));
            Assert.That(appointment.StartAt.Date, Is.EqualTo(newDate.Date));
            Assert.That(appointment.EndAt.Date, Is.EqualTo(newDate.Date));
        }
    }
}
