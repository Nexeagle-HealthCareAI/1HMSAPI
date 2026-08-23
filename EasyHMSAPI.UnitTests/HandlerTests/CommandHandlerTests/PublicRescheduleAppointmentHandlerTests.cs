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
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class PublicRescheduleAppointmentHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<ISmsService> _smsServiceMock = null!;
        private IMemoryCache _cache = null!;
        private PublicRescheduleAppointmentHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _smsServiceMock = new Mock<ISmsService>();
            _smsServiceMock.Setup(s => s.SendInvitationSmsAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
            _cache = new MemoryCache(new MemoryCacheOptions());
            _handler = new PublicRescheduleAppointmentHandler(_context, _smsServiceMock.Object, new Mock<ILogger<PublicRescheduleAppointmentHandler>>().Object, _cache);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
            _cache.Dispose();
        }

        private (Guid apptId, Guid doctorId) SeedActiveAppointment(string patientMobile = "9876543210", string statusCode = "FUTURE")
        {
            var user = TestDataFactory.SeedUser(_context, email: $"{Guid.NewGuid():N}@test.com", role: "Doctor");
            var doctor = TestDataFactory.SeedDoctor(_context, user);

            var patient = new PatientRegistration
            {
                RegistrationId = Guid.NewGuid(),
                HospitalId = Guid.NewGuid(),
                PatientId = $"PAT-{Guid.NewGuid():N}"[..12],
                FullName = "Test Patient",
                Mobile = patientMobile,
            };
            _context.PatientRegistrations.Add(patient);

            var apptId = Guid.NewGuid();
            var appt = new Appointment
            {
                ApptId = apptId,
                HospitalId = patient.HospitalId,
                DoctorId = doctor.DoctorID,
                PatientId = patient.PatientId,
                ApptDate = DateTime.UtcNow.Date.AddDays(1),
                StartAt = DateTime.UtcNow.Date.AddDays(1).AddHours(10),
                EndAt = DateTime.UtcNow.Date.AddDays(1).AddHours(10).AddMinutes(15),
                CurrentStatusCode = statusCode,
            };
            _context.Appointments.Add(appt);
            _context.SaveChanges();
            return (apptId, doctor.DoctorID);
        }

        [Test]
        public async Task Handle_ValidRequest_ReschedulesSuccessfully()
        {
            var (apptId, _) = SeedActiveAppointment();
            var newDate = DateTime.UtcNow.Date.AddDays(3);

            var response = await _handler.Handle(
                new PublicRescheduleAppointmentRequestModel { AppointmentId = apptId, Mobile = "9876543210", ToApptDate = newDate },
                CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.FinalStatus, Is.EqualTo(AppConstants.AppointmentStatus_Future));
            var updated = _context.Appointments.First(a => a.ApptId == apptId);
            Assert.That(updated.ApptDate, Is.EqualTo(newDate));
        }

        [Test]
        public async Task Handle_MismatchedMobile_ReturnsGenericFailure()
        {
            var (apptId, _) = SeedActiveAppointment(patientMobile: "9876543210");

            var response = await _handler.Handle(
                new PublicRescheduleAppointmentRequestModel { AppointmentId = apptId, Mobile = "1111111111", ToApptDate = DateTime.UtcNow.Date.AddDays(3) },
                CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Appointment not found."));
        }

        [Test]
        public async Task Handle_UnknownAppointmentId_ReturnsFailure()
        {
            var response = await _handler.Handle(
                new PublicRescheduleAppointmentRequestModel { AppointmentId = Guid.NewGuid(), Mobile = "9876543210", ToApptDate = DateTime.UtcNow.Date.AddDays(3) },
                CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Appointment not found."));
        }

        [Test]
        public async Task Handle_DateNotInFuture_ReturnsFailure()
        {
            var (apptId, _) = SeedActiveAppointment();

            var response = await _handler.Handle(
                new PublicRescheduleAppointmentRequestModel { AppointmentId = apptId, Mobile = "9876543210", ToApptDate = DateTime.UtcNow.Date },
                CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("future"));
        }

        [Test]
        public async Task Handle_AlreadyCancelled_ReturnsFailure()
        {
            var (apptId, _) = SeedActiveAppointment(statusCode: AppConstants.AppointmentStatus_Cancelled);

            var response = await _handler.Handle(
                new PublicRescheduleAppointmentRequestModel { AppointmentId = apptId, Mobile = "9876543210", ToApptDate = DateTime.UtcNow.Date.AddDays(3) },
                CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("cancelled"));
        }
    }
}
