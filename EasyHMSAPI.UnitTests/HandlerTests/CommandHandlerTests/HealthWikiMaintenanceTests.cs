using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.Services.Implementations;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using System.Collections.Generic;
using EasyHMSAPI.Application.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class HealthWikiMaintenanceTests
    {
        private AppDbContext _context = null!;
        private RecordingNotifier _n = null!;
        private CancellationToken Ct => CancellationToken.None;
        private long _mobile = 9300000000;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _n = new RecordingNotifier();
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private HealthWikiMaintenanceService Service => new(_context, _n, NullLogger<HealthWikiMaintenanceService>.Instance);

        private HealthWikiContributor Person(string name = "Dr. Meera")
        {
            var c = new HealthWikiContributor { ContributorId = Guid.NewGuid(), Type = "INDEPENDENT_DOCTOR", FullName = name, Mobile = (_mobile++).ToString(), Status = "VERIFIED", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            _context.HealthWikiContributors.Add(c);
            _context.SaveChanges();
            return c;
        }

        private HealthArticle Waiting(HealthWikiContributor reviewer, int daysAgo, string slug = "bp", string type = "MEDICAL", string status = "IN_REVIEW", bool approved = false)
        {
            var a = new HealthArticle
            {
                ArticleId = Guid.NewGuid(), Slug = slug, Title = "Living with BP", Content = "c", Type = type, Status = status, ReviewerContributorId = type == "MEDICAL" ? reviewer.ContributorId : null,
                SubmittedAt = DateTime.UtcNow.AddDays(-daysAgo), ApprovedAt = approved ? DateTime.UtcNow : null, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            _context.HealthArticles.Add(a);
            _context.SaveChanges();
            return a;
        }

        private void Told(HealthArticle a, HealthWikiContributor who, string code, int daysAgo, string status = "SENT")
        {
            _context.HealthWikiNotifications.Add(new HealthWikiNotification
            {
                NotificationId = Guid.NewGuid(), Code = code, TemplateName = "t", RecipientContributorId = who.ContributorId, EntityType = "ARTICLE", EntityId = a.ArticleId,
                Status = status, CreatedAt = DateTime.UtcNow.AddDays(-daysAgo),
            });
            _context.SaveChanges();
        }

        // ------------------------------------------------------------------------------------------ N6

        [Test]
        public async Task ADoctorWhoHasNotDecidedAfter3Days_IsReminded_WithHowLongItHasBeen()
        {
            var doctor = Person(); var a = Waiting(doctor, daysAgo: 4);
            Told(a, doctor, "N5", daysAgo: 4);

            var r = await Service.RunAsync(Ct);

            Assert.That(r.RemindersSent, Is.EqualTo(1));
            var told = _n.Of("N6").Single();
            Assert.That(told.Person, Is.EqualTo(doctor.ContributorId));
            Assert.That(told.Extra, Is.EqualTo("Living with BP|4"));
        }

        [Test]
        public async Task NotBefore3Days_NotForADoctorAlreadyApproved_NotForASectorUpdate_NotWithoutADoctor()
        {
            var doctor = Person();
            Waiting(doctor, 2, "fresh");
            Waiting(doctor, 5, "approved", approved: true);
            Waiting(doctor, 5, "sector", type: "SECTOR_UPDATE");
            Waiting(doctor, 5, "draft", status: "DRAFT");
            var unassigned = Waiting(doctor, 5, "unassigned"); unassigned.ReviewerContributorId = null; _context.SaveChanges();

            var r = await Service.RunAsync(Ct);

            Assert.That(r.RemindersSent + r.RemindersNotSent, Is.EqualTo(0));
            Assert.That(_n.Calls, Is.Empty);
        }

        [Test]
        public async Task NotMoreOftenThanEvery3Days_AndNeverMoreThan3Reminders()
        {
            var doctor = Person();
            var recent = Waiting(doctor, 10, "recent"); Told(recent, doctor, "N5", 10); Told(recent, doctor, "N6", 1);
            var due = Waiting(doctor, 10, "due"); Told(due, doctor, "N5", 10); Told(due, doctor, "N6", 4);
            var enough = Waiting(doctor, 20, "enough"); Told(enough, doctor, "N5", 20); Told(enough, doctor, "N6", 15); Told(enough, doctor, "N6", 11); Told(enough, doctor, "N6", 5);

            await Service.RunAsync(Ct);

            Assert.That(_n.Of("N6").Select(t => t.Slug), Is.EqualTo(new[] { "due" }));
        }

        [Test]
        public async Task ARunTwiceInAMoment_RemindsOnce_BecauseTheReminderIsRecorded()
        {
            var doctor = Person(); var a = Waiting(doctor, 5);
            // the real notifier records each message; the recording stand-in does not, so mimic that record
            await Service.RunAsync(Ct);
            Told(a, doctor, "N6", 0);
            await Service.RunAsync(Ct);

            Assert.That(_n.Of("N6"), Has.Exactly(1).Items);
        }

        [Test]
        public async Task ADoctorWhoseReminderWasNotTaken_IsCountedAsNotSent()
        {
            _n.ReminderAccepted = false;
            var doctor = Person(); var a = Waiting(doctor, 5);

            var r = await Service.RunAsync(Ct);

            Assert.That(r.RemindersSent, Is.EqualTo(0));
            Assert.That(r.RemindersNotSent, Is.EqualTo(1));
        }

        [Test]
        public async Task AReminderWhatsAppRefused_IsRecorded_SoTheNextDaysRunDoesNotRepeatIt_WithTheRealNotifier()
        {
            var whatsApp = new Mock<IWhatsAppMessagingService>();
            whatsApp.Setup(w => w.SendHealthWikiTemplateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<KeyValuePair<string, string>>>())).ReturnsAsync(false);
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["HealthWiki:ContributorBaseUrl"] = "https://dd.example.com" }).Build();
            var real = new HealthWikiMaintenanceService(_context, new HealthWikiNotifier(_context, whatsApp.Object, config, NullLogger<HealthWikiNotifier>.Instance), NullLogger<HealthWikiMaintenanceService>.Instance);
            var doctor = Person(); Waiting(doctor, 5);

            var first = await real.RunAsync(Ct);
            var second = await real.RunAsync(Ct);

            Assert.That(first.RemindersNotSent, Is.EqualTo(1));
            Assert.That(second.RemindersNotSent + second.RemindersSent, Is.EqualTo(0), "it was tried today and recorded, so it waits 3 days");
            whatsApp.Verify(w => w.SendHealthWikiTemplateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<KeyValuePair<string, string>>>()), Times.Once);
            Assert.That(_context.HealthWikiNotifications.Count(n => n.Code == "N6" && n.Status == "FAILED"), Is.EqualTo(1));
        }

        [Test]
        public async Task AnEditToALiveArticleThatWaitsForTheDoctor_IsRemindedToo_WithTheNewTitle()
        {
            var doctor = Person();
            var a = Waiting(doctor, 5, status: "PUBLISHED", approved: true);
            _context.HealthArticleRevisions.Add(new HealthArticleRevision { RevisionId = Guid.NewGuid(), ArticleId = a.ArticleId, Title = "Newer title", Content = "c", Status = "IN_REVIEW", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            _context.SaveChanges();

            await Service.RunAsync(Ct);

            Assert.That(_n.Of("N6").Single().Extra, Is.EqualTo("Newer title|5"));
        }

        // ------------------------------------------------------------------------------------------ "needs detail" with no reply

        private HealthWikiTopicRequest NeedsDetail(HealthWikiContributor c, int daysAgo, string title = "T")
        {
            var t = new HealthWikiTopicRequest
            {
                TopicId = Guid.NewGuid(), ContributorId = c.ContributorId, Title = title, Outline = "o", Status = "NEEDS_DETAIL", DecisionNote = "Which age group?",
                CreatedAt = DateTime.UtcNow.AddDays(-daysAgo - 1), UpdatedAt = DateTime.UtcNow.AddDays(-daysAgo),
            };
            _context.HealthWikiTopicRequests.Add(t);
            _context.SaveChanges();
            return t;
        }

        [Test]
        public async Task ARequestWithNoReplyFor30Days_IsClosedWithAReason_AndTheContributorIsTold_N11()
        {
            var c = Person("Dr. Asha");
            var old = NeedsDetail(c, 31, "Old"); var fresh = NeedsDetail(c, 10, "Fresh");

            var r = await Service.RunAsync(Ct);

            Assert.That(r.TopicsClosed, Is.EqualTo(1));
            var closed = _context.HealthWikiTopicRequests.Single(t => t.TopicId == old.TopicId);
            Assert.That(closed.Status, Is.EqualTo("DECLINED"));
            Assert.That(closed.DecisionNote, Is.EqualTo(HealthWikiMaintenanceService.AutoCloseNote));
            Assert.That(closed.DecidedByName, Is.EqualTo("System"));
            Assert.That(_context.HealthWikiTopicRequests.Single(t => t.TopicId == fresh.TopicId).Status, Is.EqualTo("NEEDS_DETAIL"));
            Assert.That(_n.Of("N11").Single().Person, Is.EqualTo(c.ContributorId));
            Assert.That(_context.HealthWikiAudits.Any(a => a.EntityId == old.TopicId && a.Action == "DECLINED"), Is.True);
        }

        [Test]
        public async Task OnlyNeedsDetailIsClosed_NotASubmittedOrAcceptedRequest_AndASecondRunClosesNothingMore()
        {
            var c = Person();
            foreach (var status in new[] { "SUBMITTED", "ACCEPTED", "ARTICLE_STARTED" })
            {
                var t = NeedsDetail(c, 60, status); t.Status = status; t.DecisionNote = null; _context.SaveChanges();
            }
            NeedsDetail(c, 40, "stale");

            var first = await Service.RunAsync(Ct);
            var second = await Service.RunAsync(Ct);

            Assert.That(first.TopicsClosed, Is.EqualTo(1));
            Assert.That(second.TopicsClosed, Is.EqualTo(0));
        }

        // ------------------------------------------------------------------------------------------ housekeeping

        [Test]
        public async Task OldSignInCodesAndEndedSessionsAreCleared_NewOnesStay()
        {
            var c = Person();
            _context.ContributorOtps.Add(new ContributorOtp { OtpId = Guid.NewGuid(), Mobile = "9100000000", CodeHash = new string('a', 64), ExpiresAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow.AddDays(-3) });
            _context.ContributorOtps.Add(new ContributorOtp { OtpId = Guid.NewGuid(), Mobile = "9100000001", CodeHash = new string('b', 64), ExpiresAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow });
            _context.ContributorSessions.Add(new ContributorSession { SessionId = Guid.NewGuid(), ContributorId = c.ContributorId, TokenHash = new string('c', 64), ExpiresAt = DateTime.UtcNow.AddDays(-40), CreatedAt = DateTime.UtcNow.AddDays(-47) });
            _context.ContributorSessions.Add(new ContributorSession { SessionId = Guid.NewGuid(), ContributorId = c.ContributorId, TokenHash = new string('d', 64), ExpiresAt = DateTime.UtcNow.AddDays(5), CreatedAt = DateTime.UtcNow });
            _context.SaveChanges();

            var r = await Service.RunAsync(Ct);

            Assert.That(r.OtpsRemoved, Is.EqualTo(1));
            Assert.That(r.SessionsRemoved, Is.EqualTo(1));
            Assert.That(_context.ContributorOtps.Count(), Is.EqualTo(1));
            Assert.That(_context.ContributorSessions.Count(), Is.EqualTo(1));
        }

        [Test]
        public async Task TheHandler_ReportsWhatTheUpkeepDid()
        {
            var doctor = Person(); Waiting(doctor, 5); NeedsDetail(doctor, 40);

            var res = await new RunHealthWikiMaintenanceHandler(Service).Handle(new RunHealthWikiMaintenanceRequestModel(), Ct);

            Assert.That(res.Success, Is.True);
            Assert.That(res.RemindersSent, Is.EqualTo(1));
            Assert.That(res.TopicsClosed, Is.EqualTo(1));
        }
    }
}
