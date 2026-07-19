using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class ConfirmPreAppointmentHandlerTests
    {
        private AppDbContext _context = null!;
        private ConfirmPreAppointmentHandler _handler = null!;
        private Mock<IWhatsAppMessagingService> _whatsAppServiceMock = null!;
        private Guid _hospitalId;
        private Doctor _doctor = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _whatsAppServiceMock = new Mock<IWhatsAppMessagingService>();
            _handler = new ConfirmPreAppointmentHandler(_context, _whatsAppServiceMock.Object, new MemoryCache(new MemoryCacheOptions()));

            _hospitalId = Guid.NewGuid();
            var user = TestDataFactory.SeedUser(_context);
            _doctor = TestDataFactory.SeedDoctor(_context, user);
            _doctor.HospitalId = _hospitalId;
            _context.SaveChanges();
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private Appointment SeedPreAppointment(DateTime preferredDate)
        {
            var appointment = new Appointment
            {
                ApptId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                DoctorId = _doctor.DoctorID,
                PatientId = "PTID00000099",
                ApptDate = preferredDate.Date,
                StartAt = preferredDate,
                EndAt = preferredDate.AddMinutes(15),
                CurrentStatusCode = AppConstants.AppointmentStatus_PreAppointment,
                BookingSource = AppConstants.BookingSource_NexeaglePublic,
                StatusHistoryJson = $"[{{\"status\":\"{AppConstants.AppointmentStatus_PreAppointment}\",\"timestamp\":\"{DateTime.UtcNow:o}\"}}]",
                LastStatusCodeAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
            };
            _context.Appointments.Add(appointment);
            _context.SaveChanges();
            return appointment;
        }

        [Test]
        public async Task Handle_ConfirmsPendingPreAppointment_AllocatesToken_ResolvesFutureStatus()
        {
            var futureDate = DateTime.Today.AddDays(3).AddHours(11);
            var appointment = SeedPreAppointment(futureDate);

            var request = new ConfirmPreAppointmentRequestModel
            {
                AppointmentId = appointment.ApptId,
                HospitalId = _hospitalId,
                StartAt = futureDate,
            };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.Status, Is.EqualTo(AppConstants.AppointmentStatus_Future));
            Assert.That(response.TokenNumber, Is.Not.Null);

            var updated = await _context.Appointments.FindAsync(appointment.ApptId);
            Assert.That(updated!.CurrentStatusCode, Is.EqualTo(AppConstants.AppointmentStatus_Future));
            Assert.That(updated.StartAt, Is.EqualTo(futureDate));

            var token = await _context.AppointmentTokens.FirstOrDefaultAsync(t => t.ApptId == appointment.ApptId);
            Assert.That(token, Is.Not.Null);
        }

        [Test]
        public async Task Handle_ConfirmsPendingPreAppointment_SendsWhatsAppConfirmation()
        {
            var futureDate = DateTime.Today.AddDays(3).AddHours(11);
            var appointment = SeedPreAppointment(futureDate);
            _context.PatientRegistrations.Add(new PatientRegistration
            {
                RegistrationId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                PatientId = appointment.PatientId,
                FullName = "Test Patient",
                Mobile = "9999999999",
            });
            await _context.SaveChangesAsync();

            _whatsAppServiceMock
                .Setup(x => x.SendAppointmentConfirmationAsync(
                    "9999999999", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            var request = new ConfirmPreAppointmentRequestModel
            {
                AppointmentId = appointment.ApptId,
                HospitalId = _hospitalId,
                StartAt = futureDate,
            };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.IsReminderSent, Is.True);
            _whatsAppServiceMock.Verify(x => x.SendAppointmentConfirmationAsync(
                "9999999999", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Once);
        }

        [Test]
        public async Task Handle_AlreadyConfirmed_RejectsDoubleConfirm()
        {
            var futureDate = DateTime.Today.AddDays(3).AddHours(11);
            var appointment = SeedPreAppointment(futureDate);
            appointment.CurrentStatusCode = AppConstants.AppointmentStatus_Future; // already confirmed
            await _context.SaveChangesAsync();

            var request = new ConfirmPreAppointmentRequestModel
            {
                AppointmentId = appointment.ApptId,
                HospitalId = _hospitalId,
                StartAt = futureDate,
            };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            var token = await _context.AppointmentTokens.FirstOrDefaultAsync(t => t.ApptId == appointment.ApptId);
            Assert.That(token, Is.Null);
        }

        [Test]
        public async Task Handle_ConflictingSlot_RejectsConfirm()
        {
            var chosenStart = DateTime.Today.AddDays(3).AddHours(11);

            // Another confirmed appointment already occupies this exact slot.
            _context.Appointments.Add(new Appointment
            {
                ApptId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                DoctorId = _doctor.DoctorID,
                PatientId = "PTID00000050",
                ApptDate = chosenStart.Date,
                StartAt = chosenStart,
                EndAt = chosenStart.AddMinutes(15),
                CurrentStatusCode = AppConstants.AppointmentStatus_Future,
                LastStatusCodeAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();

            var appointment = SeedPreAppointment(chosenStart.AddHours(2));

            var request = new ConfirmPreAppointmentRequestModel
            {
                AppointmentId = appointment.ApptId,
                HospitalId = _hospitalId,
                StartAt = chosenStart,
            };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("already booked"));
        }
    }
}
