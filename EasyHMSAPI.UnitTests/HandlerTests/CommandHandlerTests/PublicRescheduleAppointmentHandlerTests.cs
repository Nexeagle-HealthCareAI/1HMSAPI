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

        // ── AppointmentType/ValidUptoDate recompute + consult-charge reconciliation ──────────
        // Same wiring as RescheduleAppointmentHandlerTests -- this only needs to confirm the
        // bot-facing handler got the identical fix, not re-prove AppointmentTypeResolver's own logic.

        [Test]
        public async Task Handle_ChargeableRescheduledIntoFreeWindow_FlipsToFreeAndVoidsUnpaidCharge()
        {
            var user = TestDataFactory.SeedUser(_context, email: $"{Guid.NewGuid():N}@test.com", role: "Doctor");
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            var hospitalId = Guid.NewGuid();
            var patientId = $"PAT-{Guid.NewGuid():N}"[..12];

            _context.PatientRegistrations.Add(new PatientRegistration
            {
                RegistrationId = Guid.NewGuid(),
                HospitalId = hospitalId,
                PatientId = patientId,
                FullName = "Test Patient",
                Mobile = "9876543210",
            });
            _context.DoctorFees.Add(new DoctorFee
            {
                DoctorFeeId = Guid.NewGuid(),
                HospitalId = hospitalId,
                DoctorId = doctor.DoctorID,
                FeeType = "OPD_CONSULT",
                Amount = 500m,
                IsActive = true,
                FreeFollowUpDays = 10,
            });
            _context.Appointments.Add(new Appointment
            {
                ApptId = Guid.NewGuid(),
                HospitalId = hospitalId,
                DoctorId = doctor.DoctorID,
                PatientId = patientId,
                ApptDate = DateTime.Today,
                StartAt = DateTime.Today.AddHours(9),
                EndAt = DateTime.Today.AddHours(9).AddMinutes(15),
                CurrentStatusCode = AppConstants.AppointmentStatus_Completed,
                AppointmentType = AppConstants.AppointmentType_New,
            });

            var apptId = Guid.NewGuid();
            _context.Appointments.Add(new Appointment
            {
                ApptId = apptId,
                HospitalId = hospitalId,
                DoctorId = doctor.DoctorID,
                PatientId = patientId,
                ApptDate = DateTime.Today.AddDays(20),
                StartAt = DateTime.Today.AddDays(20).AddHours(10),
                EndAt = DateTime.Today.AddDays(20).AddHours(10).AddMinutes(15),
                CurrentStatusCode = "FUTURE",
                AppointmentType = AppConstants.AppointmentType_OldFee,
            });
            _context.SaveChanges();

            var encounterId = Guid.NewGuid();
            _context.Encounter.Add(new Encounter
            {
                EncounterId = encounterId,
                HospitalId = hospitalId,
                PatientId = patientId,
                EncounterTypeCode = "OPD",
                SourceType = "Appointments",
                SourceId = apptId,
                StatusCode = "OPEN",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            var chargeEventId = Guid.NewGuid();
            _context.BillingChargeEvent.Add(new BillingChargeEvent
            {
                ChargeEventId = chargeEventId,
                HospitalId = hospitalId,
                PatientId = patientId,
                EncounterId = encounterId,
                CategoryCode = "CONSULT",
                DisplayName = "Consultation",
                Qty = 1,
                UnitPrice = 500m,
                NetAmount = 500m,
                StatusCode = BillingConstants.ChargeEventStatus.Posted,
                ServiceDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            _context.SaveChanges();

            var newDate = DateTime.Today.AddDays(5); // now inside the 10-day free window
            var response = await _handler.Handle(new PublicRescheduleAppointmentRequestModel
            {
                AppointmentId = apptId,
                Mobile = "9876543210",
                ToApptDate = newDate,
                ToStartAt = newDate.AddHours(11),
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            var updated = _context.Appointments.First(a => a.ApptId == apptId);
            Assert.That(updated.AppointmentType, Is.EqualTo(AppConstants.AppointmentType_OldNoFee));

            var charge = _context.BillingChargeEvent.First(c => c.ChargeEventId == chargeEventId);
            Assert.That(charge.StatusCode, Is.EqualTo(BillingConstants.ChargeEventStatus.Void));
        }
    }
}
