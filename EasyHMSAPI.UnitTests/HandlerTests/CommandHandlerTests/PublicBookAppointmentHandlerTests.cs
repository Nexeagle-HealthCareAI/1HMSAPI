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
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class PublicBookAppointmentHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<ISmsService> _smsServiceMock = null!;
        private Mock<IWhatsAppMessagingService> _whatsAppMessagingServiceMock = null!;
        private PublicBookAppointmentHandler _handler = null!;
        private Guid _hospitalId;
        private Doctor _doctor = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _smsServiceMock = new Mock<ISmsService>();
            _whatsAppMessagingServiceMock = new Mock<IWhatsAppMessagingService>();
            _whatsAppMessagingServiceMock
                .Setup(w => w.SendAppointmentConfirmationAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);
            _handler = new PublicBookAppointmentHandler(_context, _smsServiceMock.Object, _whatsAppMessagingServiceMock.Object);

            var user = TestDataFactory.SeedUser(_context);
            var hospital = TestDataFactory.SeedHospital(_context, user.UserID, isPubliclyListed: true);
            _hospitalId = hospital.HospitalID;
            _doctor = TestDataFactory.SeedDoctor(_context, user, isPubliclyListed: true);
            TestDataFactory.SeedDoctorDepartment(_context, _doctor.DoctorID, _hospitalId);
            _context.SaveChanges();
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        [Test]
        public async Task Handle_NewPatient_CreatesPreAppointment_WithNoTokenAllocated()
        {
            var request = new PublicBookAppointmentRequestModel
            {
                DoctorId = _doctor.DoctorID,
                PreferredDate = DateTime.Today.AddDays(2),
                PreferredTime = new TimeSpan(10, 0, 0),
                Reason = "Checkup",
                Patient = new Patient { FullName = "Nexeagle Visitor", Mobile = "9998887770", Sex = "Male" },
            };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.AppointmentId, Is.Not.Null);

            var appointment = await _context.Appointments.FirstOrDefaultAsync(a => a.ApptId == response.AppointmentId);
            Assert.That(appointment, Is.Not.Null);
            Assert.That(appointment!.CurrentStatusCode, Is.EqualTo(AppConstants.AppointmentStatus_PreAppointment));
            Assert.That(appointment.BookingSource, Is.EqualTo(AppConstants.BookingSource_NexeaglePublic));

            var token = await _context.AppointmentTokens.FirstOrDefaultAsync(t => t.ApptId == appointment.ApptId);
            Assert.That(token, Is.Null, "Public booking must not allocate a token — that happens only at confirm time.");
        }

        [Test]
        public async Task Handle_NewPatient_SendsWhatsAppAppointmentConfirmation()
        {
            var request = new PublicBookAppointmentRequestModel
            {
                DoctorId = _doctor.DoctorID,
                PreferredDate = DateTime.Today.AddDays(2),
                PreferredTime = new TimeSpan(10, 0, 0),
                Patient = new Patient { FullName = "WhatsApp Visitor", Mobile = "9998887776", Sex = "Male" },
            };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.IsReminderSent, Is.True);
            _whatsAppMessagingServiceMock.Verify(w => w.SendAppointmentConfirmationAsync(
                "9998887776",
                "WhatsApp Visitor",
                "Test Hospital",
                It.IsAny<string>(),
                string.Empty,
                It.IsAny<string>(),
                It.IsAny<string>()), Times.Once);
        }

        [Test]
        public async Task Handle_ExistingPatientMatchedByMobileAndName_DoesNotDuplicate()
        {
            var existingPatient = new PatientRegistration
            {
                RegistrationId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                PatientId = "PTID00000001",
                FullName = "Repeat Visitor",
                Mobile = "9998887771",
                RegisteredAt = DateTime.UtcNow,
            };
            _context.PatientRegistrations.Add(existingPatient);
            await _context.SaveChangesAsync();

            var request = new PublicBookAppointmentRequestModel
            {
                DoctorId = _doctor.DoctorID,
                PreferredDate = DateTime.Today.AddDays(1),
                Patient = new Patient { FullName = "Repeat Visitor", Mobile = "9998887771" },
            };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.PatientId, Is.EqualTo(existingPatient.PatientId));
            var patientCount = await _context.PatientRegistrations.CountAsync(p => p.Mobile == "9998887771");
            Assert.That(patientCount, Is.EqualTo(1));
        }

        [Test]
        public async Task Handle_DoctorAtNonPubliclyListedHospital_ReturnsFailure()
        {
            var otherUser = TestDataFactory.SeedUser(_context, email: "unlisted@example.com", phone: "5555555555");
            var unlistedHospital = TestDataFactory.SeedHospital(_context, otherUser.UserID, isPubliclyListed: false);
            var doctorAtUnlistedHospital = TestDataFactory.SeedDoctor(_context, otherUser, isPubliclyListed: true);
            TestDataFactory.SeedDoctorDepartment(_context, doctorAtUnlistedHospital.DoctorID, unlistedHospital.HospitalID);
            await _context.SaveChangesAsync();

            var request = new PublicBookAppointmentRequestModel
            {
                DoctorId = doctorAtUnlistedHospital.DoctorID,
                PreferredDate = DateTime.Today.AddDays(1),
                Patient = new Patient { FullName = "Someone", Mobile = "9998887772" },
            };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(_context.Appointments.Count(), Is.EqualTo(0));
        }

        [Test]
        public async Task Handle_DoctorNotPubliclyListedThemselves_ReturnsFailure()
        {
            var otherUser = TestDataFactory.SeedUser(_context, email: "private@example.com", phone: "8888888888");
            var privateDoctor = TestDataFactory.SeedDoctor(_context, otherUser, isPubliclyListed: false);
            TestDataFactory.SeedDoctorDepartment(_context, privateDoctor.DoctorID, _hospitalId);
            await _context.SaveChangesAsync();

            var request = new PublicBookAppointmentRequestModel
            {
                DoctorId = privateDoctor.DoctorID,
                PreferredDate = DateTime.Today.AddDays(1),
                Patient = new Patient { FullName = "Someone", Mobile = "9998887798" },
            };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(_context.Appointments.Count(), Is.EqualTo(0));
        }

        [Test]
        public async Task Handle_UnknownDoctorId_ReturnsFailure()
        {
            var request = new PublicBookAppointmentRequestModel
            {
                DoctorId = Guid.NewGuid(),
                PreferredDate = DateTime.Today.AddDays(1),
                Patient = new Patient { FullName = "Someone", Mobile = "9998887799" },
            };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(_context.Appointments.Count(), Is.EqualTo(0));
        }

        [Test]
        public async Task Handle_CapturesBookingAttributionMetadata()
        {
            var request = new PublicBookAppointmentRequestModel
            {
                DoctorId = _doctor.DoctorID,
                PreferredDate = DateTime.Today.AddDays(1),
                Patient = new Patient { FullName = "Attributed Visitor", Mobile = "9998887773" },
                ReferrerUrl = "https://nexeagle.example/doctors/cardiology",
                UtmCampaign = "spring-checkup-2026",
                IpAddress = "203.0.113.42",
            };

            var response = await _handler.Handle(request, CancellationToken.None);

            var appointment = await _context.Appointments.FirstOrDefaultAsync(a => a.ApptId == response.AppointmentId);
            Assert.That(appointment!.BookingReferrerUrl, Is.EqualTo("https://nexeagle.example/doctors/cardiology"));
            Assert.That(appointment.BookingUtmCampaign, Is.EqualTo("spring-checkup-2026"));
            Assert.That(appointment.BookingIpAddress, Is.EqualTo("203.0.113.42"));
        }

        [Test]
        public async Task Handle_MarketingConsentTrue_SetsConsentAndTimestamp_OnNewPatient()
        {
            var request = new PublicBookAppointmentRequestModel
            {
                DoctorId = _doctor.DoctorID,
                PreferredDate = DateTime.Today.AddDays(1),
                Patient = new Patient { FullName = "Consenting Visitor", Mobile = "9998887774", MarketingConsent = true },
            };

            await _handler.Handle(request, CancellationToken.None);

            var patient = await _context.PatientRegistrations.FirstAsync(p => p.Mobile == "9998887774");
            Assert.That(patient.MarketingConsent, Is.True);
            Assert.That(patient.MarketingConsentAt, Is.Not.Null);
        }

        [Test]
        public async Task Handle_MarketingConsentNotSet_LeavesExistingConsentTrue_Untouched()
        {
            var consentedAt = DateTime.UtcNow.AddDays(-10);
            var existingPatient = new PatientRegistration
            {
                RegistrationId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                PatientId = "PTID00000002",
                FullName = "Already Consented",
                Mobile = "9998887775",
                RegisteredAt = DateTime.UtcNow,
                MarketingConsent = true,
                MarketingConsentAt = consentedAt,
            };
            _context.PatientRegistrations.Add(existingPatient);
            await _context.SaveChangesAsync();

            var request = new PublicBookAppointmentRequestModel
            {
                DoctorId = _doctor.DoctorID,
                PreferredDate = DateTime.Today.AddDays(1),
                Patient = new Patient { FullName = "Already Consented", Mobile = "9998887775" }, // MarketingConsent not set this time
            };

            await _handler.Handle(request, CancellationToken.None);

            var patient = await _context.PatientRegistrations.FirstAsync(p => p.Mobile == "9998887775");
            Assert.That(patient.MarketingConsent, Is.True, "A later booking that doesn't ask about consent must not revoke it.");
            Assert.That(patient.MarketingConsentAt, Is.EqualTo(consentedAt), "Timestamp of the original consent must be preserved, not overwritten.");
        }
    }
}
