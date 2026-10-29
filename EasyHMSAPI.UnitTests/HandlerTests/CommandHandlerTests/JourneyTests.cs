using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Application.Services.Models;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    /// <summary>
    /// Vita "takes you there": the patient says how they are coming and when they leave; a "time to leave" WhatsApp goes once at that
    /// time; on the way the ETA (never a location) is kept and an ETA past the appointment time marks it late; reception sees the
    /// journey only when the patient agreed.
    /// </summary>
    [TestFixture]
    public class JourneyTests
    {
        private AppDbContext _context = null!;
        private Mock<IWhatsAppMessagingService> _whatsApp = null!;
        private readonly List<(string To, string Template, string Code, IReadOnlyList<string> Buttons)> _sent = new();
        private static CancellationToken Ct => CancellationToken.None;
        private Hospital _hospital = null!;

        // India time on 26 Oct 2026: h:m IST -> UTC
        private static DateTime Utc(int day, int hour, int minute = 0) => new DateTime(2026, 10, day, hour, minute, 0, DateTimeKind.Utc).AddMinutes(-330);

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _sent.Clear();
            _whatsApp = new Mock<IWhatsAppMessagingService>();
            _whatsApp.Setup(w => w.SendTemplateWithButtonsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<IReadOnlyList<KeyValuePair<string, string>>>(), It.IsAny<IReadOnlyList<string>>()))
                .Callback<string, string, string, IReadOnlyList<KeyValuePair<string, string>>, IReadOnlyList<string>>((to, t, c, p, b) => _sent.Add((to, t, c, b)))
                .ReturnsAsync(new WhatsAppSendResult { Accepted = true });
            _hospital = new Hospital
            {
                HospitalID = Guid.NewGuid(), Name = "Salt Lake Care", Type = "General", RegistrationNumber = "REG", Contact = "03340000000",
                Location = "Sector V", City = "Kolkata", State = "WB", Country = "India", Pincode = "700091", CreatedByUserID = Guid.NewGuid(),
                Latitude = 22.58m, Longitude = 88.42m,
            };
            _context.Hospitals.Add(_hospital);
            _context.SaveChanges();
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        /// <summary>A Doctor Dekho booking for 26 Oct at <paramref name="time"/> (hospital time), booked the day before.</summary>
        private Appointment Book(TimeSpan? time, string? source = null, string? language = "hinglish")
        {
            var patientId = "P" + Guid.NewGuid().ToString("N")[..6];
            _context.PatientRegistrations.Add(new PatientRegistration { RegistrationId = Guid.NewGuid(), HospitalId = _hospital.HospitalID, PatientId = patientId, FullName = "Rina Kumari", Mobile = "9876543210" });
            var a = new Appointment
            {
                ApptId = Guid.NewGuid(), HospitalId = _hospital.HospitalID, DoctorId = Guid.NewGuid(), PatientId = patientId,
                ApptDate = new DateTime(2026, 10, 26), StartAt = new DateTime(2026, 10, 26).Add(time ?? TimeSpan.Zero),
                EndAt = new DateTime(2026, 10, 26).Add(time ?? TimeSpan.Zero).AddMinutes(15),
                CurrentStatusCode = AppConstants.AppointmentStatus_PreAppointment, BookingSource = source ?? AppConstants.BookingSource_NexeaglePublic,
                PatientLanguage = language, CreatedAt = Utc(25, 18), LastStatusCodeAt = Utc(25, 18),
            };
            _context.Appointments.Add(a);
            _context.SaveChanges();
            return a;
        }

        private Task<PublicJourneyResponseModel> Save(Guid id, string? mode = null, string? leave = null, bool? share = null, DateTime? now = null) =>
            new SavePublicJourneyHandler(_context).Handle(new SavePublicJourneyRequestModel
            { AppointmentId = id, TravelMode = mode, LeaveAt = leave, ShareWithHospital = share, NowUtc = now ?? Utc(25, 19) }, Ct);

        private Task<PublicJourneyResponseModel> Progress(Guid id, string status, int? eta = null, decimal? km = null, int? late = null, DateTime? now = null) =>
            new UpdatePublicJourneyHandler(_context).Handle(new UpdatePublicJourneyRequestModel
            { AppointmentId = id, Status = status, EtaMinutes = eta, DistanceKm = km, LateByMinutes = late, NowUtc = now ?? Utc(26, 9, 0) }, Ct);

        private Task<RunVisitMessagesResponseModel> Run(DateTime nowUtc) =>
            new RunVisitMessagesHandler(_context, _whatsApp.Object, NullLogger<RunVisitMessagesHandler>.Instance).Handle(new RunVisitMessagesRequestModel { NowUtc = nowUtc }, Ct);

        // ------------------------------------------------------------------ planning
        [Test]
        public async Task The_patient_plans_how_and_when()
        {
            var a = Book(new TimeSpan(10, 0, 0));
            var r = await Save(a.ApptId, "cab", "09:15", true);
            Assert.That(r.Success, Is.True, r.Message);
            Assert.That(r.Journey!.TravelMode, Is.EqualTo("auto_cab"));
            Assert.That(r.Journey.PlannedLeaveAt, Is.EqualTo("2026-10-26T09:15"));
            Assert.That(r.Journey.ShareWithHospital, Is.True);
            Assert.That(r.Journey.Status, Is.EqualTo("planned"));
            var again = await Save(a.ApptId, leave: "09:00");                       // fields left out keep their value
            Assert.That(again.Journey!.TravelMode, Is.EqualTo("auto_cab"));
            Assert.That(again.Journey.ShareWithHospital, Is.True);
            Assert.That(again.Journey.PlannedLeaveAt, Is.EqualTo("2026-10-26T09:00"));
        }

        [TestCase("10:30", "before the appointment")]
        [TestCase("2026-10-24T09:00", "day before")]
        [TestCase("nine", "look like")]
        public async Task A_leave_time_that_cannot_work_is_refused(string leave, string says)
        {
            var a = Book(new TimeSpan(10, 0, 0));
            var r = await Save(a.ApptId, "drive", leave);
            Assert.That(r.Success, Is.False);
            Assert.That(r.Message, Does.Contain(says));
            Assert.That(_context.AppointmentJourneys.Any(), Is.False);
        }

        [Test]
        public async Task Only_open_doctor_dekho_bookings_and_known_modes()
        {
            var walkIn = Book(new TimeSpan(10, 0, 0), source: AppConstants.BookingSource_Internal);
            Assert.That((await Save(walkIn.ApptId, "drive")).Success, Is.False);
            var a = Book(new TimeSpan(10, 0, 0));
            Assert.That((await Save(a.ApptId, "helicopter")).Message, Does.Contain("Travel mode"));
            Assert.That((await Save(Guid.NewGuid(), "drive")).Message, Is.EqualTo("Appointment not found."));
            a.CurrentStatusCode = AppConstants.AppointmentStatus_Cancelled;
            _context.SaveChanges();
            Assert.That((await Save(a.ApptId, "drive")).Success, Is.False);
        }

        // ------------------------------------------------------------------ on the way
        [Test]
        public async Task On_the_way_keeps_the_eta_and_an_eta_past_the_appointment_is_late()
        {
            var a = Book(new TimeSpan(10, 0, 0));
            await Save(a.ApptId, "drive", "09:15", true);
            var r = await Progress(a.ApptId, "on_the_way", eta: 35, km: 12.4m, now: Utc(26, 9, 15));
            Assert.That(r.Journey!.Status, Is.EqualTo("on_the_way"));
            Assert.That(r.Journey.EtaMinutes, Is.EqualTo(35));
            Assert.That(r.Journey.DistanceKm, Is.EqualTo(12.4m));
            Assert.That(r.Journey.StartedAt, Is.EqualTo(Utc(26, 9, 15)));
            var stuck = await Progress(a.ApptId, "on_the_way", eta: 40, now: Utc(26, 9, 40));   // arrives 10:20
            Assert.That(stuck.Journey!.Status, Is.EqualTo("late"));
            Assert.That(stuck.Journey.LateByMinutes, Is.EqualTo(20));
            Assert.That(stuck.Journey.StartedAt, Is.EqualTo(Utc(26, 9, 15)));                   // set once
            var better = await Progress(a.ApptId, "on_the_way", eta: 10, now: Utc(26, 9, 45));
            Assert.That(better.Journey!.Status, Is.EqualTo("on_the_way"));
            Assert.That(better.Journey.LateByMinutes, Is.Null);
        }

        [Test]
        public async Task Late_by_minutes_gives_the_hospital_an_arrival_time()
        {
            var a = Book(new TimeSpan(10, 0, 0));
            var r = await Progress(a.ApptId, "late", late: 15, now: Utc(26, 9, 30));
            Assert.That(r.Journey!.Status, Is.EqualTo("late"));
            Assert.That(r.Journey.EtaAt, Is.EqualTo(Utc(26, 10, 15)));
            Assert.That(r.Journey.EtaMinutes, Is.EqualTo(45));
        }

        [Test]
        public async Task Arrived_stays_arrived()
        {
            var a = Book(new TimeSpan(10, 0, 0));
            await Progress(a.ApptId, "arrived", now: Utc(26, 9, 50));
            var r = await Progress(a.ApptId, "on_the_way", eta: 5, now: Utc(26, 9, 55));
            Assert.That(r.Journey!.Status, Is.EqualTo("arrived"));
            Assert.That(r.Journey.EtaMinutes, Is.Null);
        }

        [TestCase(-1, null, null)]
        [TestCase(null, -2.0, null)]
        [TestCase(null, null, 700)]
        public async Task Out_of_range_values_are_refused(int? eta, double? km, int? late)
        {
            var a = Book(new TimeSpan(10, 0, 0));
            var r = await Progress(a.ApptId, "on_the_way", eta, km == null ? null : (decimal)km.Value, late);
            Assert.That(r.Success, Is.False);
        }

        [Test]
        public async Task Progress_only_around_the_day_of_the_appointment()
        {
            var a = Book(new TimeSpan(10, 0, 0));
            var r = await Progress(a.ApptId, "on_the_way", eta: 20, now: Utc(20, 9, 0));
            Assert.That(r.Success, Is.False);
            Assert.That((await Progress(a.ApptId, "unknown")).Success, Is.False);
        }

        // ------------------------------------------------------------------ "time to leave"
        [Test]
        public async Task Time_to_leave_goes_once_at_the_leave_time_with_the_navigation_link()
        {
            var a = Book(new TimeSpan(11, 0, 0));
            await Save(a.ApptId, "drive", "10:10");
            await Run(Utc(26, 10, 0));
            Assert.That(_sent.Where(s => s.Template.StartsWith("visit_leave_now")), Is.Empty);
            await Run(Utc(26, 10, 12));
            var leave = _sent.Single(s => s.Template.StartsWith("visit_leave_now"));
            Assert.That(leave.Template, Is.EqualTo("visit_leave_now_hinglish"));
            Assert.That(leave.Buttons, Is.EqualTo(new[] { "22.58,88.42", a.ApptId.ToString() }));
            await Run(Utc(26, 10, 20));
            Assert.That(_sent.Count(s => s.Template.StartsWith("visit_leave_now")), Is.EqualTo(1));
            Assert.That(_context.AppointmentJourneys.Single().LeaveReminderSentAt, Is.EqualTo(Utc(26, 10, 12)));
        }

        [Test]
        public async Task No_time_to_leave_when_already_on_the_way_or_the_moment_has_passed()
        {
            var a = Book(new TimeSpan(11, 0, 0));
            await Save(a.ApptId, "drive", "10:10");
            await Progress(a.ApptId, "on_the_way", eta: 30, now: Utc(26, 10, 5));
            var b = Book(new TimeSpan(11, 0, 0));
            await Save(b.ApptId, "walk", "09:00");
            var r = await Run(Utc(26, 10, 15));                                 // b: 75 minutes after its leave time
            Assert.That(_sent.Where(s => s.Template.StartsWith("visit_leave_now")), Is.Empty);
            Assert.That(_context.AppointmentJourneys.Single(j => j.ApptId == b.ApptId).LeaveReminderSentAt, Is.Not.Null);
            Assert.That(r.Skipped, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public async Task A_new_leave_time_gets_its_own_reminder()
        {
            var a = Book(new TimeSpan(11, 0, 0));
            await Save(a.ApptId, "drive", "09:30");
            await Run(Utc(26, 9, 31));
            await Save(a.ApptId, leave: "10:15", now: Utc(26, 9, 40));
            await Run(Utc(26, 10, 16));
            Assert.That(_sent.Count(s => s.Template.StartsWith("visit_leave_now")), Is.EqualTo(2));
        }

        // ------------------------------------------------------------------ visit page and reception
        [Test]
        public async Task The_visit_page_carries_the_journey()
        {
            var a = Book(new TimeSpan(10, 0, 0));
            await Save(a.ApptId, "transit", "09:00", false);
            var visit = await new GetPublicVisitHandler(_context, new Mock<MediatR.IMediator>().Object)
                .Handle(new GetPublicVisitRequestModel { AppointmentId = a.ApptId }, Ct);
            Assert.That(visit.Visit!.Journey!.TravelMode, Is.EqualTo("transit"));
            Assert.That(visit.Visit.Journey.PlannedLeaveAt, Is.EqualTo("2026-10-26T09:00"));
        }

        [Test]
        public async Task Reception_sees_the_journey_only_when_the_patient_agreed()
        {
            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            _context.UserProfiles.Add(new UserProfile { UserID = user.UserID, FullName = "Dr. Test" });
            var shared = Book(new TimeSpan(10, 0, 0));
            var hidden = Book(new TimeSpan(10, 30, 0));
            shared.DoctorId = hidden.DoctorId = doctor.DoctorID;
            _context.SaveChanges();
            await Save(shared.ApptId, "drive", "09:15", true);
            await Save(hidden.ApptId, "drive", "09:45", false);
            await Progress(shared.ApptId, "late", late: 10, now: Utc(26, 9, 40));
            await Progress(hidden.ApptId, "late", late: 10, now: Utc(26, 9, 40));

            var list = await new GetPatientAppointmentDetailsHandler(_context)
                .Handle(new GetPatientAppointmentDetailsRequestModel { HospitalId = _hospital.HospitalID, Status = "All" }, Ct);
            var s = list.Items.Single(i => i.AppointmentId == shared.ApptId);
            var h = list.Items.Single(i => i.AppointmentId == hidden.ApptId);
            Assert.That(s.JourneyStatus, Is.EqualTo("late"));
            Assert.That(s.JourneyLateByMinutes, Is.EqualTo(10));
            Assert.That(h.JourneyStatus, Is.Null);
            Assert.That(h.JourneyEtaAt, Is.Null);
        }
    }
}
