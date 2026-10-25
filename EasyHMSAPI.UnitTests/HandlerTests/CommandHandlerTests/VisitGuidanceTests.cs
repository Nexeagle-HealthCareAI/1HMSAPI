using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Application.Services.Models;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    /// <summary>
    /// Vita vision, Phase 4 (after booking): the reminders go out once, at the right moment, in the patient's language, only for Doctor
    /// Dekho bookings; the visit page holds the hospital's guide, check-in and token but nothing personal; only admins edit the guide.
    /// </summary>
    [TestFixture]
    public class VisitGuidanceTests
    {
        private AppDbContext _context = null!;
        private Mock<IWhatsAppMessagingService> _whatsApp = null!;
        private readonly List<(string To, string Template, string Code, IReadOnlyList<KeyValuePair<string, string>> Params, IReadOnlyList<string> Buttons)> _sent = new();
        private static CancellationToken Ct => CancellationToken.None;
        private Hospital _hospital = null!;

        // India time 2026-10-26 (a Monday): 10:00 IST = 04:30 UTC
        private static DateTime Utc(int day, int hour, int minute = 0) => new DateTime(2026, 10, day, hour, minute, 0, DateTimeKind.Utc).AddMinutes(-330);

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _sent.Clear();
            _whatsApp = new Mock<IWhatsAppMessagingService>();
            _whatsApp.Setup(w => w.SendTemplateWithButtonsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<IReadOnlyList<KeyValuePair<string, string>>>(), It.IsAny<IReadOnlyList<string>>()))
                .Callback<string, string, string, IReadOnlyList<KeyValuePair<string, string>>, IReadOnlyList<string>>((to, t, c, p, b) => _sent.Add((to, t, c, p, b)))
                .ReturnsAsync(new WhatsAppSendResult { Accepted = true });
            _hospital = new Hospital
            {
                HospitalID = Guid.NewGuid(), Name = "Salt Lake Care", Type = "General", RegistrationNumber = "REG", Contact = "03340000000",
                Location = "Sector V", City = "Kolkata", State = "WB", Country = "India", Pincode = "700091", CreatedByUserID = Guid.NewGuid(),
                Latitude = 22.58m, Longitude = 88.42m,
            };
            _context.Hospitals.Add(_hospital);
            _context.HospitalVisitGuides.Add(new HospitalVisitGuide { HospitalId = _hospital.HospitalID, Landmark = "Opposite City Centre", OpdLocation = "OPD on the 1st floor", UpdatedAt = DateTime.UtcNow });
            _context.SaveChanges();
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        /// <summary>A Doctor Dekho booking for 26 Oct (hospital time), booked on <paramref name="bookedDay"/> at 18:00 IST.</summary>
        private Appointment Book(TimeSpan? time, string? language = null, int bookedDay = 25, string? source = null, string? status = null)
        {
            var patientId = "P" + Guid.NewGuid().ToString("N")[..6];
            _context.PatientRegistrations.Add(new PatientRegistration { RegistrationId = Guid.NewGuid(), HospitalId = _hospital.HospitalID, PatientId = patientId, FullName = "Rina Kumari", Mobile = "9876543210" });
            var a = new Appointment
            {
                ApptId = Guid.NewGuid(), HospitalId = _hospital.HospitalID, DoctorId = Guid.NewGuid(), PatientId = patientId,
                ApptDate = new DateTime(2026, 10, 26), StartAt = new DateTime(2026, 10, 26).Add(time ?? TimeSpan.Zero),
                EndAt = new DateTime(2026, 10, 26).Add(time ?? TimeSpan.Zero).AddMinutes(15),
                CurrentStatusCode = status ?? AppConstants.AppointmentStatus_PreAppointment, BookingSource = source ?? AppConstants.BookingSource_NexeaglePublic,
                PatientLanguage = language, CreatedAt = Utc(bookedDay, 18), LastStatusCodeAt = Utc(bookedDay, 18),
            };
            _context.Appointments.Add(a);
            _context.SaveChanges();
            return a;
        }

        private Task<RunVisitMessagesResponseModel> Run(DateTime nowUtc) =>
            new RunVisitMessagesHandler(_context, _whatsApp.Object, NullLogger<RunVisitMessagesHandler>.Instance).Handle(new RunVisitMessagesRequestModel { NowUtc = nowUtc }, Ct);

        // ------------------------------------------------------------------ reminders
        [Test]
        public async Task Morning_then_two_hours_before_each_once_in_the_patients_language()
        {
            var a = Book(new TimeSpan(11, 0, 0), "hinglish");
            await Run(Utc(26, 7, 0));                                             // before 07:30: nothing
            Assert.That(_sent, Is.Empty);
            await Run(Utc(26, 7, 35));
            Assert.That(_sent.Single().Template, Is.EqualTo("visit_reminder_morning_hinglish"));
            Assert.That(_sent[0].Code, Is.EqualTo("en"));
            Assert.That(_sent[0].Buttons, Is.EqualTo(new[] { "22.58,88.42", a.ApptId.ToString() }));
            Assert.That(_sent[0].Params.First(p => p.Key == "directions").Value, Is.EqualTo("Opposite City Centre. OPD on the 1st floor"));
            await Run(Utc(26, 8, 0));
            Assert.That(_sent, Has.Count.EqualTo(1));                             // once
            await Run(Utc(26, 9, 5));                                             // 11:00 - 2 h
            Assert.That(_sent.Last().Template, Is.EqualTo("visit_reminder_soon_hinglish"));
            Assert.That(_sent, Has.Count.EqualTo(2));
            await Run(Utc(26, 10, 0));
            Assert.That(_sent, Has.Count.EqualTo(2));
        }

        [Test]
        public async Task A_booking_without_a_time_gets_only_the_morning_reminder()
        {
            Book(null, "hi");
            await Run(Utc(26, 8, 0));
            await Run(Utc(26, 14, 0));
            Assert.That(_sent.Select(s => s.Template), Is.EqualTo(new[] { "visit_reminder_morning_hi" }));
            Assert.That(_sent[0].Code, Is.EqualTo("hi"));
        }

        [Test]
        public async Task Booked_the_same_morning_gets_no_morning_reminder_and_late_bookings_no_two_hour_one()
        {
            var a = Book(new TimeSpan(10, 0, 0));
            a.CreatedAt = Utc(26, 9, 0);                                          // booked the same morning, at 09:00, for 10:00
            _context.SaveChanges();
            var r = await Run(Utc(26, 9, 10));
            Assert.That(_sent, Is.Empty);
            Assert.That(r.Skipped, Is.EqualTo(1));
            await Run(Utc(26, 9, 20));
            Assert.That(_sent, Is.Empty);
        }

        [Test]
        public async Task Walk_ins_and_cancelled_visits_get_nothing()
        {
            Book(new TimeSpan(11, 0, 0), source: AppConstants.BookingSource_Internal);
            Book(new TimeSpan(11, 0, 0), status: AppConstants.AppointmentStatus_Cancelled);
            await Run(Utc(26, 8, 0));
            await Run(Utc(26, 9, 30));
            Assert.That(_sent, Is.Empty);
        }

        [Test]
        public async Task After_the_visit_two_hours_later_but_not_at_night()
        {
            var a = Book(new TimeSpan(18, 0, 0), status: AppConstants.AppointmentStatus_Completed);
            a.MorningReminderSentAt = a.BeforeVisitReminderSentAt = Utc(26, 8, 0);
            a.LastStatusCodeAt = Utc(26, 20, 0);                                   // completed 20:00 IST
            _context.SaveChanges();
            await Run(Utc(26, 21, 0));                                            // only 1 h
            await Run(Utc(26, 23, 0));                                            // 2 h passed, but 23:00 is quiet hours
            Assert.That(_sent, Is.Empty);
            await Run(Utc(27, 8, 30));
            Assert.That(_sent.Single().Template, Is.EqualTo("visit_after_en"));
            Assert.That(_sent[0].Buttons, Is.EqualTo(new[] { a.ApptId.ToString() }));
        }

        [Test]
        public async Task A_refused_message_is_not_retried_every_few_minutes()
        {
            _whatsApp.Setup(w => w.SendTemplateWithButtonsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<IReadOnlyList<KeyValuePair<string, string>>>(), It.IsAny<IReadOnlyList<string>>()))
                .ReturnsAsync(WhatsAppSendResult.Refused);
            Book(new TimeSpan(11, 0, 0));
            var first = await Run(Utc(26, 8, 0));
            var second = await Run(Utc(26, 8, 5));
            Assert.That(first.NotSent, Is.EqualTo(1));
            Assert.That(second.NotSent + second.Sent, Is.EqualTo(0));
        }

        [Test]
        public void Languages_and_template_names()
        {
            Assert.That(VisitMessages.Template(VisitMessages.Confirmation, "hi"), Is.EqualTo(("visit_confirmation_hi", "hi")));
            Assert.That(VisitMessages.Template(VisitMessages.Confirmation, "Hinglish"), Is.EqualTo(("visit_confirmation_hinglish", "en")));
            Assert.That(VisitMessages.Template(VisitMessages.Confirmation, null), Is.EqualTo(("visit_confirmation_en", "en")));
            Assert.That(VisitMessages.CleanLanguage("bn"), Is.EqualTo("en"));
            Assert.That(VisitMessages.CleanLanguage(""), Is.Null);
        }

        // ------------------------------------------------------------------ the visit page
        [Test]
        public async Task The_visit_page_has_the_guide_and_check_in_today_but_nothing_personal()
        {
            var a = Book(new TimeSpan(11, 0, 0), "hi");
            a.ApptDate = VisitMessages.ToIst(DateTime.UtcNow).Date;               // today
            a.StartAt = a.ApptDate.AddHours(11);
            _context.SaveChanges();
            var mediator = new Mock<IMediator>();
            var r = await new GetPublicVisitHandler(_context, mediator.Object).Handle(new GetPublicVisitRequestModel { AppointmentId = a.ApptId }, Ct);
            var v = r.Visit!;
            Assert.That(v.Reference, Is.EqualTo(a.ApptId.ToString("N")[..8].ToUpperInvariant()));
            Assert.That(v.HospitalName, Is.EqualTo("Salt Lake Care"));
            Assert.That(v.Guide.OpdLocation, Is.EqualTo("OPD on the 1st floor"));
            Assert.That(v.Time, Is.EqualTo("11:00"));
            Assert.That(v.IsToday && v.CanCheckIn, Is.True);
            Assert.That(v.Confirmed, Is.False);
            Assert.That(v.MapUrl, Does.EndWith("destination=22.58,88.42"));
            Assert.That(v.Token, Is.Null);
            var json = System.Text.Json.JsonSerializer.Serialize(v);
            Assert.That(json, Does.Not.Contain("Rina").And.Not.Contain("9876543210"));
        }

        [Test]
        public async Task Checked_in_shows_the_token_and_the_wait()
        {
            var a = Book(new TimeSpan(11, 0, 0));
            a.ApptDate = VisitMessages.ToIst(DateTime.UtcNow).Date;
            _context.AppointmentTokens.Add(new AppointmentToken { TokenId = Guid.NewGuid(), HospitalId = a.HospitalId, DoctorId = a.DoctorId, ApptId = a.ApptId, TokenDate = a.ApptDate, TokenNo = 7, CreatedAt = DateTime.UtcNow });
            _context.SaveChanges();
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<GetQueueTokenStatusRequestModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GetQueueTokenStatusResponseModel { Success = true, TokenNo = 7, Status = "WAITING", CurrentServingTokenNo = 4, PositionInQueue = 3, EstimatedWaitMinutes = 25 });
            var v = (await new GetPublicVisitHandler(_context, mediator.Object).Handle(new GetPublicVisitRequestModel { AppointmentId = a.ApptId }, Ct)).Visit!;
            Assert.That(v.CanCheckIn, Is.False);
            Assert.That(v.Token!.TokenNo, Is.EqualTo(7));
            Assert.That(v.Token.EstimatedWaitMinutes, Is.EqualTo(25));
        }

        [Test]
        public async Task Unknown_appointment()
        {
            var r = await new GetPublicVisitHandler(_context, new Mock<IMediator>().Object).Handle(new GetPublicVisitRequestModel { AppointmentId = Guid.NewGuid() }, Ct);
            Assert.That(r.Success, Is.False);
        }

        // ------------------------------------------------------------------ the guide
        [Test]
        public async Task Only_an_admin_saves_the_guide_and_empty_fields_are_dropped()
        {
            var admin = HrAuthSeed.SeedMember(_context, _hospital.HospitalID, "admin_panel");
            var member = HrAuthSeed.SeedMember(_context, _hospital.HospitalID);
            var save = new SaveHospitalVisitGuideHandler(_context);
            var denied = await save.Handle(new SaveHospitalVisitGuideRequestModel { HospitalId = _hospital.HospitalID, CallerUserId = member, OpdLocation = "x" }, Ct);
            Assert.That(denied.Success, Is.False);
            var ok = await save.Handle(new SaveHospitalVisitGuideRequestModel
            {
                HospitalId = _hospital.HospitalID, CallerUserId = admin, RegistrationDesk = "  Counter 3,\n ground floor ", OpdLocation = "   ",
            }, Ct);
            Assert.That(ok.Success && ok.HasMapPin, Is.True);
            Assert.That(ok.Guide.RegistrationDesk, Is.EqualTo("Counter 3, ground floor"));
            Assert.That(ok.Guide.OpdLocation, Is.Null);
            var read = await new GetHospitalVisitGuideHandler(_context).Handle(new GetHospitalVisitGuideRequestModel { HospitalId = _hospital.HospitalID, CallerUserId = member }, Ct);
            Assert.That(read.Guide.RegistrationDesk, Is.EqualTo("Counter 3, ground floor"));
        }
    }
}
