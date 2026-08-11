using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class ResolveCheckInHandlerTests
    {
        private AppDbContext _context = null!;
        private ResolveCheckInHandler _handler = null!;
        private Guid _hospitalId;
        private Guid _doctorId;
        private const string Mobile = "9876543210";

        // Kolkata coordinates -- Hospital.Latitude/Longitude in tests below.
        private const decimal HospitalLat = 22.5726m;
        private const decimal HospitalLng = 88.3639m;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new ResolveCheckInHandler(_context);
            _hospitalId = Guid.NewGuid();
            _doctorId = Guid.NewGuid();
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private void SeedHospitalWithLocation(decimal? lat = HospitalLat, decimal? lng = HospitalLng)
        {
            var user = TestDataFactory.SeedUser(_context);
            _context.Hospitals.Add(new Hospital
            {
                HospitalID = _hospitalId,
                Name = "Test Hospital",
                Type = "General",
                RegistrationNumber = "REG1",
                Contact = "9999999999",
                Location = "Somewhere",
                City = "Kolkata",
                State = "WB",
                Country = "India",
                Pincode = "700001",
                Latitude = lat,
                Longitude = lng,
                CreatedByUserID = user.UserID,
                CreatedAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow,
            });
            _context.SaveChanges();
        }

        private void SeedPatientRegistration(string patientId, string mobile = Mobile)
        {
            _context.PatientRegistrations.Add(new PatientRegistration
            {
                RegistrationId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                PatientId = patientId,
                Mobile = mobile,
                FullName = "Test Patient",
                RegisteredAt = DateTime.UtcNow,
            });
            _context.SaveChanges();
        }

        private Appointment SeedAppointment(string patientId, DateTime? startAt = null, string status = "PRE_APPOINTMENT", Guid? doctorId = null)
        {
            var appointment = new Appointment
            {
                ApptId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                DoctorId = doctorId ?? _doctorId,
                PatientId = patientId,
                ApptDate = (startAt ?? DateTime.UtcNow).Date,
                StartAt = startAt ?? DateTime.UtcNow,
                EndAt = (startAt ?? DateTime.UtcNow).AddMinutes(15),
                CurrentStatusCode = status,
                LastStatusCodeAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
            };
            _context.Appointments.Add(appointment);
            _context.SaveChanges();
            return appointment;
        }

        private ResolveCheckInRequestModel Request(decimal lat = HospitalLat, decimal lng = HospitalLng, string mobile = Mobile) =>
            new() { HospitalId = _hospitalId, Mobile = mobile, Latitude = lat, Longitude = lng };

        [Test]
        public async Task Handle_OutsideGeofence_RejectsBeforeAnyMatchWouldHaveBeenFound()
        {
            SeedHospitalWithLocation();
            SeedPatientRegistration("PT001");
            SeedAppointment("PT001");

            // Roughly Delhi -- far outside a 200m radius from the Kolkata hospital coordinates.
            var response = await _handler.Handle(Request(lat: 28.6139m, lng: 77.2090m), CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.AppointmentId, Is.Null);
            Assert.That(await _context.AppointmentTokens.CountAsync(), Is.EqualTo(0));
        }

        [Test]
        public async Task Handle_NoPatientRegistrationForMobile_ReturnsNoAppointmentFound()
        {
            SeedHospitalWithLocation();

            var response = await _handler.Handle(Request(), CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("No appointment found"));
        }

        [Test]
        public async Task Handle_NoAppointmentToday_ReturnsNoAppointmentFound()
        {
            SeedHospitalWithLocation();
            SeedPatientRegistration("PT001");
            SeedAppointment("PT001", startAt: DateTime.UtcNow.AddDays(1));

            var response = await _handler.Handle(Request(), CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("No appointment found"));
        }

        [Test]
        public async Task Handle_CancelledAppointmentToday_IsExcluded()
        {
            SeedHospitalWithLocation();
            SeedPatientRegistration("PT001");
            SeedAppointment("PT001", status: AppConstants.AppointmentStatus_Cancelled);

            var response = await _handler.Handle(Request(), CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Candidates, Is.Null);
        }

        [Test]
        public async Task Handle_SingleMatchToday_IssuesTokenAndReturnsAppointmentId()
        {
            SeedHospitalWithLocation();
            SeedPatientRegistration("PT001");
            var appt = SeedAppointment("PT001");

            var response = await _handler.Handle(Request(), CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.AppointmentId, Is.EqualTo(appt.ApptId));
            Assert.That(response.TokenNo, Is.EqualTo(1));

            var token = await _context.AppointmentTokens.FirstAsync(t => t.ApptId == appt.ApptId);
            Assert.That(token.ArrivalMethod, Is.EqualTo(AppConstants.QueueArrivalMethod_Geofence));
        }

        [Test]
        public async Task Handle_RepeatedResolve_IsIdempotent_DoesNotDoubleAllocate()
        {
            SeedHospitalWithLocation();
            SeedPatientRegistration("PT001");
            SeedAppointment("PT001");

            var first = await _handler.Handle(Request(), CancellationToken.None);
            var second = await _handler.Handle(Request(), CancellationToken.None);

            Assert.That(first.TokenNo, Is.EqualTo(second.TokenNo));
            Assert.That(await _context.AppointmentTokens.CountAsync(), Is.EqualTo(1));
        }

        [Test]
        public async Task Handle_MultipleAppointmentsToday_ReturnsCandidatesWithoutIssuingToken()
        {
            SeedHospitalWithLocation();
            SeedPatientRegistration("PT001");
            var doctorId2 = Guid.NewGuid();
            var first = SeedAppointment("PT001", startAt: DateTime.UtcNow.AddHours(1));
            var second = SeedAppointment("PT001", startAt: DateTime.UtcNow.AddHours(3), doctorId: doctorId2);

            var response = await _handler.Handle(Request(), CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Candidates, Is.Not.Null);
            Assert.That(response.Candidates!.Count, Is.EqualTo(2));
            var candidateIds = response.Candidates.Select(c => c.AppointmentId).ToList();
            Assert.That(candidateIds, Is.EquivalentTo(new[] { first.ApptId, second.ApptId }));
            Assert.That(await _context.AppointmentTokens.CountAsync(), Is.EqualTo(0));
        }

        [Test]
        public async Task Handle_HospitalHasNoLocationConfigured_Rejects()
        {
            SeedHospitalWithLocation(lat: null, lng: null);
            SeedPatientRegistration("PT001");
            SeedAppointment("PT001");

            var response = await _handler.Handle(Request(), CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(await _context.AppointmentTokens.CountAsync(), Is.EqualTo(0));
        }

        [Test]
        public async Task Handle_MissingHospitalIdOrMobile_Rejects()
        {
            var response = await _handler.Handle(new ResolveCheckInRequestModel { HospitalId = Guid.Empty, Mobile = "", Latitude = HospitalLat, Longitude = HospitalLng }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
        }
    }
}
