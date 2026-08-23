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
    public class PublicCancelAppointmentHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<ISmsService> _smsServiceMock = null!;
        private IMemoryCache _cache = null!;
        private PublicCancelAppointmentHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _smsServiceMock = new Mock<ISmsService>();
            _smsServiceMock.Setup(s => s.SendInvitationSmsAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
            _cache = new MemoryCache(new MemoryCacheOptions());
            _handler = new PublicCancelAppointmentHandler(_context, _smsServiceMock.Object, new Mock<ILogger<PublicCancelAppointmentHandler>>().Object, _cache);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
            _cache.Dispose();
        }

        // Seeds an active doctor + a live (FUTURE) appointment for a patient with the given
        // mobile, and returns the ApptId so tests can act on it.
        private Guid SeedActiveAppointment(string patientMobile = "9876543210", string statusCode = "FUTURE")
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
            return apptId;
        }

        [Test]
        public async Task Handle_ValidAppointmentAndMobile_CancelsSuccessfully()
        {
            var apptId = SeedActiveAppointment();

            var response = await _handler.Handle(
                new PublicCancelAppointmentRequestModel { AppointmentId = apptId, Mobile = "9876543210", Reason = "Change of plans" },
                CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.FinalStatus, Is.EqualTo(AppConstants.AppointmentStatus_Cancelled));
            var updated = _context.Appointments.First(a => a.ApptId == apptId);
            Assert.That(updated.CurrentStatusCode, Is.EqualTo(AppConstants.AppointmentStatus_Cancelled));
            Assert.That(updated.CancellationReason, Is.EqualTo("Change of plans"));
        }

        [Test]
        public async Task Handle_MobileWithCountryCodeAndSpacing_StillMatches()
        {
            // WhatsApp numbers and hospital-entered mobiles rarely share the exact same format —
            // the handler normalizes both sides before comparing.
            var apptId = SeedActiveAppointment(patientMobile: "9876543210");

            var response = await _handler.Handle(
                new PublicCancelAppointmentRequestModel { AppointmentId = apptId, Mobile = "+91 98765 43210" },
                CancellationToken.None);

            Assert.That(response.Success, Is.True);
        }

        [Test]
        public async Task Handle_EmptyMobile_ReturnsFailure()
        {
            var apptId = SeedActiveAppointment();

            var response = await _handler.Handle(
                new PublicCancelAppointmentRequestModel { AppointmentId = apptId, Mobile = "" },
                CancellationToken.None);

            Assert.That(response.Success, Is.False);
            var unchanged = _context.Appointments.First(a => a.ApptId == apptId);
            Assert.That(unchanged.CurrentStatusCode, Is.EqualTo("FUTURE"));
        }

        [Test]
        public async Task Handle_MismatchedMobile_ReturnsGenericFailure_NoPiiLeak()
        {
            var apptId = SeedActiveAppointment(patientMobile: "9876543210");

            var response = await _handler.Handle(
                new PublicCancelAppointmentRequestModel { AppointmentId = apptId, Mobile = "9999999999" },
                CancellationToken.None);

            Assert.That(response.Success, Is.False);
            // Same generic message as "not found" — must not reveal that the AppointmentId was
            // valid but the mobile didn't match, which would let a caller enumerate valid IDs.
            Assert.That(response.Message, Is.EqualTo("Appointment not found."));
            var unchanged = _context.Appointments.First(a => a.ApptId == apptId);
            Assert.That(unchanged.CurrentStatusCode, Is.EqualTo("FUTURE"));
        }

        [Test]
        public async Task Handle_UnknownAppointmentId_ReturnsFailure()
        {
            var response = await _handler.Handle(
                new PublicCancelAppointmentRequestModel { AppointmentId = Guid.NewGuid(), Mobile = "9876543210" },
                CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Appointment not found."));
        }

        [Test]
        public async Task Handle_AlreadyCancelled_ReturnsFailure()
        {
            var apptId = SeedActiveAppointment(statusCode: AppConstants.AppointmentStatus_Cancelled);

            var response = await _handler.Handle(
                new PublicCancelAppointmentRequestModel { AppointmentId = apptId, Mobile = "9876543210" },
                CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("already cancelled"));
        }

        [Test]
        public async Task Handle_AlreadyCompleted_ReturnsFailure()
        {
            var apptId = SeedActiveAppointment(statusCode: AppConstants.AppointmentStatus_Completed);

            var response = await _handler.Handle(
                new PublicCancelAppointmentRequestModel { AppointmentId = apptId, Mobile = "9876543210" },
                CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("Cannot cancel a completed"));
        }
    }
}
