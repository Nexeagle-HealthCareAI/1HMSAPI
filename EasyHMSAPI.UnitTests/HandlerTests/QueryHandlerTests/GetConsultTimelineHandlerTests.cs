using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class GetConsultTimelineHandlerTests
    {
        private AppDbContext _context = null!;
        private GetConsultTimelineHandler _handler = null!;
        private Guid _hospitalId;
        private Guid _doctorId;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetConsultTimelineHandler(_context);
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
        public async Task Handle_BackdatedTargetDate_AnchorsOnLastVisitBeforeTargetDate_NotAFutureOne()
        {
            // Reproduces the live bug: a patient has an early paid visit, then a LATER paid visit
            // gets booked (e.g. out of chronological order), then a THIRD, backdated booking lands
            // between the two. The preview for that backdated booking must anchor "Last paid" /
            // "Free until" on the visit actually before it in time, not on the chronologically-later
            // one just because it has the larger ApptDate.
            var patient = SeedPatient();
            SeedFreeFollowUpDays(3);
            var early = DateTime.Today.AddDays(-20);
            var future = DateTime.Today.AddDays(20);
            SeedAppointment(patient.PatientId!, early, AppConstants.AppointmentType_New);
            SeedAppointment(patient.PatientId!, future, AppConstants.AppointmentType_OldFee);

            var targetDate = DateTime.Today.AddDays(-10); // between `early` and `future`

            var request = new GetConsultTimelineRequestModel
            {
                HospitalId = _hospitalId,
                PatientId = patient.PatientId!,
                DoctorId = _doctorId,
                TargetDate = targetDate,
            };

            var response = await _handler.Handle(request, CancellationToken.None);

            // A handler that didn't bound History to ApptDate <= targetDate would instead pick
            // `future` (the max ApptDate overall) as the anchor here.
            Assert.That(response.LastFeeVisit, Is.Not.Null);
            Assert.That(response.LastFeeVisit!.ApptDate, Is.EqualTo(early));
            Assert.That(response.ValidUptoDate, Is.EqualTo(early.AddDays(3)));
        }

        [Test]
        public async Task Handle_NoFutureAppointments_StillReturnsLastFeeVisit()
        {
            var patient = SeedPatient();
            SeedFreeFollowUpDays(7);
            var lastVisit = DateTime.Today.AddDays(-3);
            SeedAppointment(patient.PatientId!, lastVisit, AppConstants.AppointmentType_New);

            var request = new GetConsultTimelineRequestModel
            {
                HospitalId = _hospitalId,
                PatientId = patient.PatientId!,
                DoctorId = _doctorId,
                TargetDate = DateTime.Today,
            };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.LastFeeVisit, Is.Not.Null);
            Assert.That(response.LastFeeVisit!.ApptDate, Is.EqualTo(lastVisit));
            Assert.That(response.ValidUptoDate, Is.EqualTo(lastVisit.AddDays(7)));
        }
    }
}
