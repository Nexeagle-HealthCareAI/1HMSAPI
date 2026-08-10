using System;
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
    public class IssueQueueTokenHandlerTests
    {
        private AppDbContext _context = null!;
        private IssueQueueTokenHandler _handler = null!;
        private Guid _hospitalId;
        private Guid _doctorId;

        // Kolkata coordinates -- Hospital.Latitude/Longitude in tests below.
        private const decimal HospitalLat = 22.5726m;
        private const decimal HospitalLng = 88.3639m;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new IssueQueueTokenHandler(_context);
            _hospitalId = Guid.NewGuid();
            _doctorId = Guid.NewGuid();
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private Appointment SeedAppointment(DateTime? startAt = null, string status = "PRE_APPOINTMENT")
        {
            var appointment = new Appointment
            {
                ApptId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                DoctorId = _doctorId,
                PatientId = "PT001",
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

        private void SeedHospitalWithLocation(decimal? lat = HospitalLat, decimal? lng = HospitalLng)
        {
            var user = TestDataFactory.SeedUser(_context);
            _context.Hospitals.Add(new EasyHMSAPI.Domain.Entities.Hospital
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

        [Test]
        public async Task Handle_WithinGeofence_IssuesToken()
        {
            SeedHospitalWithLocation();
            var appt = SeedAppointment();

            var response = await _handler.Handle(new IssueQueueTokenRequestModel { AppointmentId = appt.ApptId, Latitude = HospitalLat, Longitude = HospitalLng }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.TokenNo, Is.EqualTo(1));
            Assert.That(response.Status, Is.EqualTo(AppConstants.QueueTokenStatus_Waiting));

            var token = await _context.AppointmentTokens.FirstAsync(t => t.ApptId == appt.ApptId);
            Assert.That(token.ArrivalMethod, Is.EqualTo(AppConstants.QueueArrivalMethod_Geofence));
            Assert.That(token.QueueSequence, Is.EqualTo(1));
            Assert.That(token.ArrivedAt, Is.Not.Null);
        }

        [Test]
        public async Task Handle_OutsideGeofence_Rejects()
        {
            SeedHospitalWithLocation();
            var appt = SeedAppointment();

            // Roughly Delhi -- far outside a 200m radius from the Kolkata hospital coordinates.
            var response = await _handler.Handle(new IssueQueueTokenRequestModel { AppointmentId = appt.ApptId, Latitude = 28.6139m, Longitude = 77.2090m }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(await _context.AppointmentTokens.CountAsync(), Is.EqualTo(0));
        }

        [Test]
        public async Task Handle_RetriedScan_IsIdempotent_DoesNotDoubleAllocate()
        {
            SeedHospitalWithLocation();
            var appt = SeedAppointment();

            var first = await _handler.Handle(new IssueQueueTokenRequestModel { AppointmentId = appt.ApptId, Latitude = HospitalLat, Longitude = HospitalLng }, CancellationToken.None);
            var second = await _handler.Handle(new IssueQueueTokenRequestModel { AppointmentId = appt.ApptId, Latitude = HospitalLat, Longitude = HospitalLng }, CancellationToken.None);

            Assert.That(first.TokenNo, Is.EqualTo(second.TokenNo));
            Assert.That(await _context.AppointmentTokens.CountAsync(t => t.ApptId == appt.ApptId), Is.EqualTo(1));
        }

        [Test]
        public async Task Handle_CancelledAppointment_Rejects()
        {
            SeedHospitalWithLocation();
            var appt = SeedAppointment(status: AppConstants.AppointmentStatus_Cancelled);

            var response = await _handler.Handle(new IssueQueueTokenRequestModel { AppointmentId = appt.ApptId, Latitude = HospitalLat, Longitude = HospitalLng }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
        }

        [Test]
        public async Task Handle_HospitalHasNoLocationConfigured_Rejects()
        {
            SeedHospitalWithLocation(lat: null, lng: null);
            var appt = SeedAppointment();

            var response = await _handler.Handle(new IssueQueueTokenRequestModel { AppointmentId = appt.ApptId, Latitude = HospitalLat, Longitude = HospitalLng }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
        }

        [Test]
        public async Task Handle_HybridOrdering_EarlierSlotCheckingInLater_QueuesAhead()
        {
            SeedHospitalWithLocation();
            var later = SeedAppointment(startAt: DateTime.UtcNow.AddHours(2));
            var earlier = SeedAppointment(startAt: DateTime.UtcNow.AddHours(1));

            // The later-slot patient checks in first...
            await _handler.Handle(new IssueQueueTokenRequestModel { AppointmentId = later.ApptId, Latitude = HospitalLat, Longitude = HospitalLng }, CancellationToken.None);
            // ...then the earlier-slot patient checks in second.
            await _handler.Handle(new IssueQueueTokenRequestModel { AppointmentId = earlier.ApptId, Latitude = HospitalLat, Longitude = HospitalLng }, CancellationToken.None);

            var earlierToken = await _context.AppointmentTokens.FirstAsync(t => t.ApptId == earlier.ApptId);
            var laterToken = await _context.AppointmentTokens.FirstAsync(t => t.ApptId == later.ApptId);

            // Despite checking in second, the earlier slot must have a lower (earlier) QueueSequence.
            Assert.That(earlierToken.QueueSequence, Is.LessThan(laterToken.QueueSequence));
        }
    }
}
