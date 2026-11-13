using System;
using System.Linq;
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
    public class GetRecentOnlineBookingsHandlerTests
    {
        private AppDbContext _context = null!;
        private GetRecentOnlineBookingsHandler _handler = null!;
        private Guid _hospitalId;
        private Doctor _doctor = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetRecentOnlineBookingsHandler(_context);
            _hospitalId = Guid.NewGuid();

            var user = TestDataFactory.SeedUser(_context);
            _doctor = TestDataFactory.SeedDoctor(_context, user);
            _context.UserProfiles.Add(new UserProfile { UserID = user.UserID, FullName = "Dr. Test" });
            _context.PatientRegistrations.Add(new PatientRegistration { PatientId = "PAT1", FullName = "Asha Verma" });
            _context.SaveChanges();
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        private Appointment AddAppointment(DateTime createdAt, string? bookingSource, Guid? hospitalId = null)
        {
            var appointment = new Appointment
            {
                ApptId = Guid.NewGuid(),
                HospitalId = hospitalId ?? _hospitalId,
                DoctorId = _doctor.DoctorID,
                PatientId = "PAT1",
                ApptDate = DateTime.Today.AddDays(1),
                StartAt = DateTime.Today.AddDays(1).AddHours(10),
                CurrentStatusCode = AppConstants.AppointmentStatus_PreAppointment,
                BookingSource = bookingSource,
                CreatedAt = createdAt,
            };
            _context.Appointments.Add(appointment);
            _context.SaveChanges();
            return appointment;
        }

        [Test]
        public async Task Handle_WithoutSince_ReturnsOnlyTheCursor_NotHistoricalBookings()
        {
            AddAppointment(DateTime.UtcNow.AddMinutes(-2), AppConstants.BookingSource_NexeaglePublic);

            var response = await _handler.Handle(new GetRecentOnlineBookingsRequestModel { HospitalId = _hospitalId }, CancellationToken.None);

            Assert.That(response.Items, Is.Empty);
            Assert.That(response.ServerTime, Is.EqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(5)));
        }

        [Test]
        public async Task Handle_ReturnsOnlyPublicBookingsForThisHospitalCreatedAfterTheCursor()
        {
            var cursor = DateTime.UtcNow.AddMinutes(-1);
            var expected = AddAppointment(DateTime.UtcNow.AddSeconds(-10), AppConstants.BookingSource_NexeaglePublic);
            AddAppointment(DateTime.UtcNow.AddSeconds(-10), null);                                                   // walk-in / staff-created
            AddAppointment(DateTime.UtcNow.AddSeconds(-10), "SOMETHING_ELSE");                                        // other channel
            AddAppointment(DateTime.UtcNow.AddSeconds(-10), AppConstants.BookingSource_NexeaglePublic, Guid.NewGuid()); // another hospital
            AddAppointment(DateTime.UtcNow.AddMinutes(-30), AppConstants.BookingSource_NexeaglePublic);              // long before the cursor

            var response = await _handler.Handle(new GetRecentOnlineBookingsRequestModel { HospitalId = _hospitalId, Since = cursor }, CancellationToken.None);

            Assert.That(response.Items.Select(i => i.AppointmentId), Is.EqualTo(new[] { expected.ApptId }));
        }

        [Test]
        public async Task Handle_ResolvesPatientAndDoctorNames_AndOrdersNewestFirst()
        {
            var older = AddAppointment(DateTime.UtcNow.AddSeconds(-40), AppConstants.BookingSource_NexeaglePublic);
            var newer = AddAppointment(DateTime.UtcNow.AddSeconds(-5), AppConstants.BookingSource_NexeaglePublic);

            var response = await _handler.Handle(
                new GetRecentOnlineBookingsRequestModel { HospitalId = _hospitalId, Since = DateTime.UtcNow.AddMinutes(-1) }, CancellationToken.None);

            Assert.That(response.Items.Select(i => i.AppointmentId), Is.EqualTo(new[] { newer.ApptId, older.ApptId }));
            Assert.That(response.Items[0].PatientName, Is.EqualTo("Asha Verma"));
            Assert.That(response.Items[0].DoctorName, Is.EqualTo("Dr. Test"));
            Assert.That(response.Items[0].Status, Is.EqualTo(AppConstants.AppointmentStatus_PreAppointment));
        }

        [Test]
        public async Task Handle_RereadsASmallWindowBeforeTheCursor_SoLateCommittedRowsAreNotLost()
        {
            var cursor = DateTime.UtcNow;
            var justBeforeCursor = AddAppointment(cursor.AddSeconds(-10), AppConstants.BookingSource_NexeaglePublic); // inside the overlap
            AddAppointment(cursor.AddSeconds(-60), AppConstants.BookingSource_NexeaglePublic);                         // outside it

            var response = await _handler.Handle(new GetRecentOnlineBookingsRequestModel { HospitalId = _hospitalId, Since = cursor }, CancellationToken.None);

            Assert.That(response.Items.Select(i => i.AppointmentId), Is.EqualTo(new[] { justBeforeCursor.ApptId }));
        }

        [Test]
        public async Task Handle_CapsTheLookback_SoAStaleCursorDoesNotReplayOldBookings()
        {
            AddAppointment(DateTime.UtcNow.AddHours(-30), AppConstants.BookingSource_NexeaglePublic); // beyond the 12h cap
            var recent = AddAppointment(DateTime.UtcNow.AddHours(-2), AppConstants.BookingSource_NexeaglePublic);

            var response = await _handler.Handle(
                new GetRecentOnlineBookingsRequestModel { HospitalId = _hospitalId, Since = DateTime.UtcNow.AddDays(-3) }, CancellationToken.None);

            Assert.That(response.Items.Select(i => i.AppointmentId), Is.EqualTo(new[] { recent.ApptId }));
        }

        [Test]
        public async Task Handle_ReturnsAtMostTwentyItems()
        {
            for (var i = 0; i < 25; i++)
                AddAppointment(DateTime.UtcNow.AddSeconds(-i), AppConstants.BookingSource_NexeaglePublic);

            var response = await _handler.Handle(
                new GetRecentOnlineBookingsRequestModel { HospitalId = _hospitalId, Since = DateTime.UtcNow.AddMinutes(-5) }, CancellationToken.None);

            Assert.That(response.Items, Has.Count.EqualTo(20));
        }
    }
}
