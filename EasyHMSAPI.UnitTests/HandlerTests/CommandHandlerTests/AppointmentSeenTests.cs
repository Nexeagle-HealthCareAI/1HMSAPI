using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    /// <summary>"Hospital has seen it": recorded once, by the first staff action on a public request.</summary>
    [TestFixture]
    public class AppointmentSeenTests
    {
        private AppDbContext _context = null!;
        private Guid _hospitalId;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _hospitalId = Guid.NewGuid();
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private Appointment SeedRequest()
        {
            var at = DateTime.Today.AddDays(2).AddHours(10);
            var a = new Appointment
            {
                ApptId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                DoctorId = Guid.NewGuid(),
                PatientId = "PTID00000077",
                ApptDate = at.Date,
                StartAt = at,
                EndAt = at.AddMinutes(15),
                CurrentStatusCode = AppConstants.AppointmentStatus_PreAppointment,
                BookingSource = AppConstants.BookingSource_NexeaglePublic,
                LastStatusCodeAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
            };
            _context.Appointments.Add(a);
            _context.SaveChanges();
            return a;
        }

        private Alert SeedAlert(string code, string? sourceRefId)
        {
            var alert = new Alert
            {
                AlertId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                AlertCode = code,
                Title = "New online booking",
                RaisedAt = DateTime.UtcNow,
                SourceRefId = sourceRefId,
            };
            _context.Alert.Add(alert);
            _context.SaveChanges();
            return alert;
        }

        [Test]
        public void Mark_OnlyTheFirstTimeCounts()
        {
            var a = new Appointment();
            var first = new DateTime(2026, 10, 24, 5, 0, 0, DateTimeKind.Utc);
            var user = Guid.NewGuid();

            Assert.That(AppointmentSeen.Mark(a, user, first), Is.True);
            Assert.That(AppointmentSeen.Mark(a, Guid.NewGuid(), first.AddHours(1)), Is.False);

            Assert.That(a.HospitalSeenAt, Is.EqualTo(first));
            Assert.That(a.HospitalSeenByUserId, Is.EqualTo(user));
        }

        [Test]
        public void Mark_EmptyUserIsStoredAsUnknown()
        {
            var a = new Appointment();
            AppointmentSeen.Mark(a, Guid.Empty);
            Assert.That(a.HospitalSeenAt, Is.Not.Null);
            Assert.That(a.HospitalSeenByUserId, Is.Null);
        }

        [Test]
        public async Task AcknowledgingTheOnlineBookingAlert_MarksTheRequestSeen()
        {
            var appt = SeedRequest();
            var alert = SeedAlert(AppointmentSeen.OnlineRequestAlertCode, appt.ApptId.ToString());
            var staff = Guid.NewGuid();

            var res = await new AcknowledgeAlertHandler(_context).Handle(
                new AcknowledgeAlertRequestModel { HospitalId = _hospitalId, AlertId = alert.AlertId, LoggedInUserId = staff, LoggedInUserName = "Front desk" },
                CancellationToken.None);

            Assert.That(res.Success, Is.True);
            var saved = await _context.Appointments.AsNoTracking().SingleAsync(a => a.ApptId == appt.ApptId);
            Assert.That(saved.HospitalSeenAt, Is.Not.Null);
            Assert.That(saved.HospitalSeenByUserId, Is.EqualTo(staff));
        }

        [Test]
        public async Task AcknowledgingAnOtherAlert_LeavesTheRequestUnseen()
        {
            var appt = SeedRequest();
            var alert = SeedAlert("LICENSE_EXPIRY_30", appt.ApptId.ToString());

            await new AcknowledgeAlertHandler(_context).Handle(
                new AcknowledgeAlertRequestModel { HospitalId = _hospitalId, AlertId = alert.AlertId, LoggedInUserId = Guid.NewGuid() },
                CancellationToken.None);

            var saved = await _context.Appointments.AsNoTracking().SingleAsync(a => a.ApptId == appt.ApptId);
            Assert.That(saved.HospitalSeenAt, Is.Null);
        }

        [Test]
        public async Task MarkSeenEndpoint_IsIdempotent_AndKeepsTheFirstTime()
        {
            var appt = SeedRequest();
            var handler = new MarkAppointmentSeenHandler(_context);

            var first = await handler.Handle(new MarkAppointmentSeenRequestModel { AppointmentId = appt.ApptId, HospitalId = _hospitalId, LoggedInUserId = Guid.NewGuid() }, CancellationToken.None);
            var second = await handler.Handle(new MarkAppointmentSeenRequestModel { AppointmentId = appt.ApptId, HospitalId = _hospitalId, LoggedInUserId = Guid.NewGuid() }, CancellationToken.None);

            Assert.That(first.Success && second.Success, Is.True);
            Assert.That(first.HospitalSeenAt, Is.Not.Null);
            Assert.That(second.HospitalSeenAt, Is.EqualTo(first.HospitalSeenAt));
        }

        [Test]
        public async Task MarkSeenEndpoint_AnotherHospitalsRequest_IsNotFound()
        {
            var appt = SeedRequest();

            var res = await new MarkAppointmentSeenHandler(_context).Handle(
                new MarkAppointmentSeenRequestModel { AppointmentId = appt.ApptId, HospitalId = Guid.NewGuid() },
                CancellationToken.None);

            Assert.That(res.Success, Is.False);
            var saved = await _context.Appointments.AsNoTracking().SingleAsync(a => a.ApptId == appt.ApptId);
            Assert.That(saved.HospitalSeenAt, Is.Null);
        }
    }
}
