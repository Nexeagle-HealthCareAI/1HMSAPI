using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.ServiceTests
{
    [TestFixture]
    public class AppointmentTypeResolverTests
    {
        private AppDbContext _context = null!;
        private Guid _hospitalId;
        private Guid _doctorId;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _hospitalId = Guid.NewGuid();
            _doctorId = Guid.NewGuid();
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        private void SeedFreeFollowUpDays(int days)
        {
            _context.DoctorFees.Add(new DoctorFee
            {
                DoctorFeeId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                DoctorId = _doctorId,
                FeeType = "OPD_CONSULT",
                Amount = 500m,
                FreeFollowUpDays = days,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            _context.SaveChanges();
        }

        private PatientRegistration SeedPatient(string patientId = "P-TEST-001")
        {
            var patient = new PatientRegistration
            {
                RegistrationId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                PatientId = patientId,
                FullName = "Test Patient",
                Mobile = "9876543210",
            };
            _context.PatientRegistrations.Add(patient);
            _context.SaveChanges();
            return patient;
        }

        private Appointment SeedAppointment(string patientId, DateTime apptDate, string? appointmentType)
        {
            var appt = new Appointment
            {
                ApptId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                DoctorId = _doctorId,
                PatientId = patientId,
                ApptDate = apptDate,
                StartAt = apptDate,
                EndAt = apptDate.AddMinutes(15),
                CurrentStatusCode = AppConstants.AppointmentStatus_Completed,
                AppointmentType = appointmentType,
                LastStatusCodeAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
            };
            _context.Appointments.Add(appt);
            _context.SaveChanges();
            return appt;
        }

        [Test]
        public async Task ResolveAsync_NoMatchingPatient_ReturnsNew()
        {
            var result = await AppointmentTypeResolver.ResolveAsync(
                _context, _hospitalId, "NO-SUCH-PATIENT", null, null,
                _doctorId, DateTime.Today, null, CancellationToken.None);

            Assert.That(result.AppointmentType, Is.EqualTo(AppConstants.AppointmentType_New));
            Assert.That(result.FeeApplies, Is.True);
        }

        [Test]
        public async Task ResolveAsync_ExistingPatientNoPriorAppointmentForDoctor_ReturnsNew()
        {
            var patient = SeedPatient();

            var result = await AppointmentTypeResolver.ResolveAsync(
                _context, _hospitalId, patient.PatientId, null, null,
                _doctorId, DateTime.Today, null, CancellationToken.None);

            Assert.That(result.AppointmentType, Is.EqualTo(AppConstants.AppointmentType_New));
        }

        [Test]
        public async Task ResolveAsync_WithinFreeFollowUpWindow_ReturnsOldNoFee()
        {
            var patient = SeedPatient();
            SeedFreeFollowUpDays(7);
            var lastVisit = DateTime.Today.AddDays(-3);
            SeedAppointment(patient.PatientId!, lastVisit, AppConstants.AppointmentType_New);

            var result = await AppointmentTypeResolver.ResolveAsync(
                _context, _hospitalId, patient.PatientId, null, null,
                _doctorId, DateTime.Today, null, CancellationToken.None);

            Assert.That(result.AppointmentType, Is.EqualTo(AppConstants.AppointmentType_OldNoFee));
            Assert.That(result.FeeApplies, Is.False);
            Assert.That(result.ValidUptoDate, Is.EqualTo(lastVisit.AddDays(7)));
        }

        [Test]
        public async Task ResolveAsync_OutsideFreeFollowUpWindow_ReturnsOldFee()
        {
            var patient = SeedPatient();
            SeedFreeFollowUpDays(7);
            var lastVisit = DateTime.Today.AddDays(-10);
            SeedAppointment(patient.PatientId!, lastVisit, AppConstants.AppointmentType_New);

            var result = await AppointmentTypeResolver.ResolveAsync(
                _context, _hospitalId, patient.PatientId, null, null,
                _doctorId, DateTime.Today, null, CancellationToken.None);

            Assert.That(result.AppointmentType, Is.EqualTo(AppConstants.AppointmentType_OldFee));
            Assert.That(result.FeeApplies, Is.True);
        }

        [Test]
        public async Task ResolveAsync_FreeFollowUpDaysZero_AlwaysChargeable()
        {
            // Confirms the chosen default polarity: an unconfigured/0 free-follow-up window means
            // every return visit is chargeable, even the very next day - the opposite of the old
            // PrescriptionSetting.ValidDuration=0 ("never expires") behavior this replaces.
            var patient = SeedPatient();
            SeedFreeFollowUpDays(0);
            var lastVisit = DateTime.Today.AddDays(-1);
            SeedAppointment(patient.PatientId!, lastVisit, AppConstants.AppointmentType_New);

            var result = await AppointmentTypeResolver.ResolveAsync(
                _context, _hospitalId, patient.PatientId, null, null,
                _doctorId, DateTime.Today, null, CancellationToken.None);

            Assert.That(result.AppointmentType, Is.EqualTo(AppConstants.AppointmentType_OldFee));
            Assert.That(result.ValidUptoDate, Is.Null);
        }

        [Test]
        public async Task ResolveAsync_NoDoctorFeeConfigured_DefaultsToAlwaysChargeable()
        {
            var patient = SeedPatient();
            // No DoctorFee row seeded at all - must behave exactly like FreeFollowUpDays = 0.
            var lastVisit = DateTime.Today.AddDays(-1);
            SeedAppointment(patient.PatientId!, lastVisit, AppConstants.AppointmentType_New);

            var result = await AppointmentTypeResolver.ResolveAsync(
                _context, _hospitalId, patient.PatientId, null, null,
                _doctorId, DateTime.Today, null, CancellationToken.None);

            Assert.That(result.AppointmentType, Is.EqualTo(AppConstants.AppointmentType_OldFee));
        }

        [Test]
        public async Task ResolveAsync_UpdatingAppointmentId_ExcludesItselfFromLastAppointmentLookup()
        {
            var patient = SeedPatient();
            SeedFreeFollowUpDays(30);
            var self = SeedAppointment(patient.PatientId!, DateTime.Today, AppConstants.AppointmentType_New);

            var result = await AppointmentTypeResolver.ResolveAsync(
                _context, _hospitalId, null, patient.PatientId, null,
                _doctorId, DateTime.Today, self.ApptId, CancellationToken.None);

            // With no OTHER prior appointment, excluding itself must fall back to "no prior visit".
            Assert.That(result.AppointmentType, Is.EqualTo(AppConstants.AppointmentType_New));
        }

        [Test]
        public async Task ResolveAsync_IgnoresAppointmentsAfterTargetDate()
        {
            var patient = SeedPatient();
            SeedFreeFollowUpDays(3);
            var early = DateTime.Today.AddDays(-20);
            var future = DateTime.Today.AddDays(20);
            SeedAppointment(patient.PatientId!, early, AppConstants.AppointmentType_New);
            SeedAppointment(patient.PatientId!, future, AppConstants.AppointmentType_OldFee);

            var targetDate = DateTime.Today.AddDays(-10); // past `early`'s 3-day window, well before `future`

            var result = await AppointmentTypeResolver.ResolveAsync(
                _context, _hospitalId, patient.PatientId, null, null,
                _doctorId, targetDate, null, CancellationToken.None);

            // A resolver that ignored ApptDate <= targetDate would instead pick `future` (the max
            // ApptDate overall) and incorrectly report a still-open free window.
            Assert.That(result.AppointmentType, Is.EqualTo(AppConstants.AppointmentType_OldFee));
            Assert.That(result.ValidUptoDate, Is.EqualTo(targetDate.AddDays(3)));
        }
    }
}
