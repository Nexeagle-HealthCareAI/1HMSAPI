using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class GetAdmissionReferralsHandlerTests
    {
        private AppDbContext _context = null!;
        private GetAdmissionReferralsHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetAdmissionReferralsHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ReturnsReferrals_WithPatientAndDoctorNames()
        {
            var hospitalId = Guid.NewGuid();
            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            _context.UserProfiles.Add(new UserProfile { UserProfileID = Guid.NewGuid(), UserID = user.UserID, FullName = "Dr Jane Doe", UpdatedAt = DateTime.UtcNow });
            _context.PatientRegistrations.Add(new PatientRegistration { RegistrationId = Guid.NewGuid(), HospitalId = hospitalId, PatientId = "PAT123", FullName = "John Smith", Mobile = "9999999999" });

            _context.AdmissionReferrals.Add(new AdmissionReferral
            {
                ReferralId = Guid.NewGuid(), HospitalId = hospitalId, PatientId = "PAT123",
                ReferringDoctorId = doctor.DoctorID, CaseType = "PLANNED", StatusCode = "PENDING",
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetAdmissionReferralsRequestModel { HospitalId = hospitalId }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.Referrals, Has.Count.EqualTo(1));
            Assert.That(response.Referrals[0].PatientName, Is.EqualTo("John Smith"));
            Assert.That(response.Referrals[0].ReferringDoctorName, Is.EqualTo("Dr Jane Doe"));
        }

        [Test]
        public async Task Handle_FlagsSourceAppointmentCancelled_OnlyForPendingReferrals()
        {
            var hospitalId = Guid.NewGuid();
            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            var cancelledApptId = Guid.NewGuid();
            var activeApptId = Guid.NewGuid();

            _context.Appointments.Add(new Appointment
            {
                ApptId = cancelledApptId, HospitalId = hospitalId, DoctorId = doctor.DoctorID, PatientId = "PAT1",
                ApptDate = DateTime.UtcNow.Date, StartAt = DateTime.UtcNow, EndAt = DateTime.UtcNow.AddMinutes(30),
                CurrentStatusCode = "CANCELLED",
            });
            _context.Appointments.Add(new Appointment
            {
                ApptId = activeApptId, HospitalId = hospitalId, DoctorId = doctor.DoctorID, PatientId = "PAT2",
                ApptDate = DateTime.UtcNow.Date, StartAt = DateTime.UtcNow, EndAt = DateTime.UtcNow.AddMinutes(30),
                CurrentStatusCode = "FUTURE",
            });

            // PENDING referral whose source appointment is cancelled — should be flagged.
            _context.AdmissionReferrals.Add(new AdmissionReferral
            {
                ReferralId = Guid.NewGuid(), HospitalId = hospitalId, PatientId = "PAT1", AppointmentId = cancelledApptId,
                ReferringDoctorId = doctor.DoctorID, CaseType = "PLANNED", StatusCode = "PENDING",
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            // CONVERTED referral whose source appointment is cancelled — already a terminal outcome, not flagged.
            _context.AdmissionReferrals.Add(new AdmissionReferral
            {
                ReferralId = Guid.NewGuid(), HospitalId = hospitalId, PatientId = "PAT1", AppointmentId = cancelledApptId,
                ReferringDoctorId = doctor.DoctorID, CaseType = "PLANNED", StatusCode = "CONVERTED",
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            // PENDING referral whose source appointment is still active — not flagged.
            _context.AdmissionReferrals.Add(new AdmissionReferral
            {
                ReferralId = Guid.NewGuid(), HospitalId = hospitalId, PatientId = "PAT2", AppointmentId = activeApptId,
                ReferringDoctorId = doctor.DoctorID, CaseType = "PLANNED", StatusCode = "PENDING",
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetAdmissionReferralsRequestModel { HospitalId = hospitalId }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.Referrals, Has.Count.EqualTo(3));
            Assert.That(response.Referrals.Single(r => r.StatusCode == "PENDING" && r.PatientId == "PAT1").SourceAppointmentCancelled, Is.True);
            Assert.That(response.Referrals.Single(r => r.StatusCode == "CONVERTED").SourceAppointmentCancelled, Is.False);
            Assert.That(response.Referrals.Single(r => r.PatientId == "PAT2").SourceAppointmentCancelled, Is.False);
        }

        [Test]
        public async Task Handle_FilterByStatus_ReturnsOnlyMatching()
        {
            var hospitalId = Guid.NewGuid();
            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);

            _context.AdmissionReferrals.Add(new AdmissionReferral { ReferralId = Guid.NewGuid(), HospitalId = hospitalId, PatientId = "PAT1", ReferringDoctorId = doctor.DoctorID, CaseType = "PLANNED", StatusCode = "PENDING", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            _context.AdmissionReferrals.Add(new AdmissionReferral { ReferralId = Guid.NewGuid(), HospitalId = hospitalId, PatientId = "PAT2", ReferringDoctorId = doctor.DoctorID, CaseType = "PLANNED", StatusCode = "NOT_ADMITTED", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetAdmissionReferralsRequestModel { HospitalId = hospitalId, StatusCode = "PENDING" }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.Referrals, Has.Count.EqualTo(1));
            Assert.That(response.Referrals[0].PatientId, Is.EqualTo("PAT1"));
        }

        [Test]
        public async Task Handle_FilterByPatientId_ReturnsOnlyThatPatientsReferrals()
        {
            var hospitalId = Guid.NewGuid();
            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);

            _context.AdmissionReferrals.Add(new AdmissionReferral { ReferralId = Guid.NewGuid(), HospitalId = hospitalId, PatientId = "PAT1", ReferringDoctorId = doctor.DoctorID, CaseType = "PLANNED", StatusCode = "PENDING", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            _context.AdmissionReferrals.Add(new AdmissionReferral { ReferralId = Guid.NewGuid(), HospitalId = hospitalId, PatientId = "PAT2", ReferringDoctorId = doctor.DoctorID, CaseType = "PLANNED", StatusCode = "PENDING", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetAdmissionReferralsRequestModel { HospitalId = hospitalId, PatientId = "PAT1" }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.Referrals, Has.Count.EqualTo(1));
            Assert.That(response.Referrals[0].PatientId, Is.EqualTo("PAT1"));
        }

        [Test]
        public async Task Handle_ConvertedReferral_JoinsAdmittedAtFromAdmission()
        {
            var hospitalId = Guid.NewGuid();
            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            var admissionId = Guid.NewGuid();
            var admittedAt = new DateTime(2026, 7, 5, 10, 30, 0, DateTimeKind.Utc);

            _context.Admission.Add(new Admission
            {
                AdmissionId = admissionId, HospitalId = hospitalId, PatientId = "PAT1",
                AdmissionNo = "ADM-1", AdmittedAt = admittedAt, StatusCode = "ADMITTED",
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            _context.AdmissionReferrals.Add(new AdmissionReferral
            {
                ReferralId = Guid.NewGuid(), HospitalId = hospitalId, PatientId = "PAT1",
                ReferringDoctorId = doctor.DoctorID, CaseType = "PLANNED", StatusCode = "CONVERTED",
                ConvertedAdmissionId = admissionId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            // PENDING referral with no ConvertedAdmissionId — AdmittedAt should stay null.
            _context.AdmissionReferrals.Add(new AdmissionReferral
            {
                ReferralId = Guid.NewGuid(), HospitalId = hospitalId, PatientId = "PAT2",
                ReferringDoctorId = doctor.DoctorID, CaseType = "PLANNED", StatusCode = "PENDING",
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetAdmissionReferralsRequestModel { HospitalId = hospitalId }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.Referrals.Single(r => r.PatientId == "PAT1").AdmittedAt, Is.EqualTo(admittedAt));
            Assert.That(response.Referrals.Single(r => r.PatientId == "PAT2").AdmittedAt, Is.Null);
        }
    }
}
