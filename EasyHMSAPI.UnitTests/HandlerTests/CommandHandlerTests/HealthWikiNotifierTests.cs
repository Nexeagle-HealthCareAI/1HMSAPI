using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Application.Services.Implementations;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Application.Services.Models;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class HealthWikiNotifierTests
    {
        private record Sent(string Mobile, string Template, Dictionary<string, string> Params);

        private AppDbContext _context = null!;
        private Mock<IWhatsAppMessagingService> _whatsApp = null!;
        private readonly List<Sent> _sent = new();
        private bool _accept;
        private Exception? _throws;
        private CancellationToken Ct => CancellationToken.None;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _sent.Clear();
            _accept = true;
            _throws = null;
            _whatsApp = new Mock<IWhatsAppMessagingService>();
            _whatsApp.Setup(w => w.SendHealthWikiTemplateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<KeyValuePair<string, string>>>()))
                .Returns((string mobile, string template, IReadOnlyList<KeyValuePair<string, string>> p) =>
                {
                    if (_throws != null) throw _throws;
                    _sent.Add(new Sent(mobile, template, p.ToDictionary(x => x.Key, x => x.Value)));
                    return Task.FromResult(_accept);
                });
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private HealthWikiNotifier Notifier(string? doctorDekho = "https://dd.example.com/", string? easyHms = "https://1hms.example.com") =>
            new(_context, _whatsApp.Object, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["HealthWiki:ContributorBaseUrl"] = doctorDekho, ["WebApp:BaseUrl"] = easyHms,
            }).Build(), NullLogger<HealthWikiNotifier>.Instance);

        private HealthWikiContributor Person(string name = "Dr. Asha Rao", string type = "INDEPENDENT_DOCTOR", string? mobile = "9891122002", Guid? doctorId = null)
        {
            var c = new HealthWikiContributor
            {
                ContributorId = Guid.NewGuid(), Type = type, FullName = name, Mobile = mobile, Status = "VERIFIED", DoctorId = doctorId,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, RejectReason = "The registration number could not be found.",
            };
            _context.HealthWikiContributors.Add(c);
            _context.SaveChanges();
            return c;
        }

        private static HealthArticleRef Article(string slug = "high-blood-pressure", string title = "Living with High Blood Pressure") => new(Guid.NewGuid(), slug, title);

        private static HealthWikiTopicRequest Topic(Guid contributorId, string note = "Which age group does this cover?") => new()
        {
            TopicId = Guid.NewGuid(), ContributorId = contributorId, Title = "Silent heart attack", Outline = "o", Status = "DECLINED", DecisionNote = note,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };

        // ------------------------------------------------------------------------------------------ Meta matches names, so names are tested

        [Test]
        public void TheTemplateNames_AreTheOnesCreatedInWhatsAppManager()
        {
            Assert.That(HealthWikiConstants.Template.Invite, Is.EqualTo("health_wiki_invite_1"));
            Assert.That(HealthWikiConstants.Template.ProfileApproved, Is.EqualTo("health_wiki_profile_approved"));
            Assert.That(HealthWikiConstants.Template.ProfileRejected, Is.EqualTo("health_wiki_profile_rejected"));
            Assert.That(HealthWikiConstants.Template.ReviewRequest, Is.EqualTo("health_wiki_review_request"));
            Assert.That(HealthWikiConstants.Template.ReviewReminder, Is.EqualTo("health_wiki_review_reminder"));
            Assert.That(HealthWikiConstants.Template.ChangesRequested, Is.EqualTo("health_wiki_changes_requested"));
            Assert.That(HealthWikiConstants.Template.ArticlePublished, Is.EqualTo("health_wiki_article_published"));
            Assert.That(HealthWikiConstants.Template.TopicAccepted, Is.EqualTo("health_wiki_topic_accepted"));
            Assert.That(HealthWikiConstants.Template.TopicDeclined, Is.EqualTo("health_wiki_topic_declined"));
            Assert.That(HealthWikiConstants.Template.TopicNeedsDetail, Is.EqualTo("health_wiki_topic_needs_detail"));
            Assert.That(HealthWikiConstants.Template.ArticleRemoved, Is.EqualTo("health_wiki_article_removed"));
            Assert.That(HealthWikiConstants.Template.RegistrationReceived, Is.EqualTo("health_wiki_registration_received"));
        }

        [Test]
        public async Task EveryTemplate_IsSentWithExactlyTheVariableNamesItWasCreatedWith()
        {
            var n = Notifier();
            var asha = Person(); var author = Person("Priya Menon", mobile: "9891122003");
            var a = Article();

            await n.ProfileApprovedAsync(asha, Ct);
            await n.ProfileRejectedAsync(asha, Ct);
            await n.RegistrationReceivedAsync(asha, Ct);
            await n.ReviewRequestedAsync(a, asha.ContributorId, Ct);
            await n.ReviewReminderAsync(Article("b"), asha.ContributorId, 4, Ct);
            await n.ChangesRequestedAsync(Article("c"), author.ContributorId, "Dr. Meera Nair", "Add when to see a doctor.", false, Ct);
            await n.PublishedAsync(Article("d"), author.ContributorId, null, Ct);
            await n.ArticleRemovedAsync(Article("e"), author.ContributorId, null, "Wrong dose.", Ct);
            await n.TopicAcceptedAsync(Topic(asha.ContributorId), "silent-heart-attack", Ct);
            await n.TopicDeclinedAsync(Topic(asha.ContributorId), Ct);
            await n.TopicNeedsDetailAsync(Topic(asha.ContributorId), Ct);

            var expected = new Dictionary<string, string[]>
            {
                [HealthWikiConstants.Template.ProfileApproved] = new[] { "contributor_name", "url" },
                [HealthWikiConstants.Template.ProfileRejected] = new[] { "contributor_name", "reason" },
                [HealthWikiConstants.Template.RegistrationReceived] = new[] { "contributor_name" },
                [HealthWikiConstants.Template.ReviewRequest] = new[] { "reviewer_name", "article_title", "url" },
                [HealthWikiConstants.Template.ReviewReminder] = new[] { "reviewer_name", "article_title", "days", "url" },
                [HealthWikiConstants.Template.ChangesRequested] = new[] { "author_name", "requested_by", "article_title", "comment", "url" },
                [HealthWikiConstants.Template.ArticlePublished] = new[] { "person_name", "article_title", "url" },
                [HealthWikiConstants.Template.ArticleRemoved] = new[] { "person_name", "article_title", "reason" },
                [HealthWikiConstants.Template.TopicAccepted] = new[] { "contributor_name", "topic_title", "url" },
                [HealthWikiConstants.Template.TopicDeclined] = new[] { "contributor_name", "topic_title", "reason" },
                [HealthWikiConstants.Template.TopicNeedsDetail] = new[] { "contributor_name", "topic_title", "request", "url" },
            };
            foreach (var (template, names) in expected)
            {
                var call = _sent.SingleOrDefault(s => s.Template == template);
                Assert.That(call, Is.Not.Null, $"{template} was not sent");
                Assert.That(call!.Params.Keys, Is.EquivalentTo(names), template);
                Assert.That(call.Params.Values, Has.None.Empty, $"{template} has an empty variable");
            }
        }

        [Test]
        public async Task TheVariablesCarryTheRealValues()
        {
            var author = Person("Priya Menon", mobile: "9891122003");
            await Notifier().ChangesRequestedAsync(Article(), author.ContributorId, "Dr. Meera Nair", "Add when to see a doctor.", false, Ct);

            var p = _sent.Single().Params;
            Assert.That(p["author_name"], Is.EqualTo("Priya Menon"));
            Assert.That(p["requested_by"], Is.EqualTo("Dr. Meera Nair"));
            Assert.That(p["article_title"], Is.EqualTo("Living with High Blood Pressure"));
            Assert.That(p["comment"], Is.EqualTo("Add when to see a doctor."));
            Assert.That(p["url"], Is.EqualTo("https://dd.example.com/contribute/write/high-blood-pressure"));
            Assert.That(_sent.Single().Mobile, Is.EqualTo("9891122003"));
        }

        // ------------------------------------------------------------------------------------------ who gets which link

        [Test]
        public async Task AHospitalDoctorIsSentToEasyHms_AnIndependentDoctorToDoctorDekho_AndTheirNumberComesFromTheirEasyHmsUser()
        {
            var userId = Guid.NewGuid(); var doctorId = Guid.NewGuid();
            _context.Users.Add(new User { UserID = userId, MobileNumber = "+91 98765 43210", UserStatusId = 1 });
            _context.Doctors.Add(new Doctor { DoctorID = doctorId, UserID = userId, LicenseNumber = "L1" });
            await _context.SaveChangesAsync();
            var hospital = Person("Dr. Hospital", "HOSPITAL_DOCTOR", mobile: null, doctorId: doctorId);
            var independent = Person("Dr. Independent");

            await Notifier().ReviewRequestedAsync(Article(), hospital.ContributorId, Ct);
            await Notifier().ReviewRequestedAsync(Article("other"), independent.ContributorId, Ct);

            var forHospital = _sent.Single(s => s.Params["reviewer_name"] == "Dr. Hospital");
            Assert.That(forHospital.Mobile, Is.EqualTo("9876543210"), "the number is read from the EasyHMS user, not copied onto the contributor");
            Assert.That(forHospital.Params["url"], Is.EqualTo("https://1hms.example.com/health-wiki/high-blood-pressure"));
            Assert.That(_sent.Single(s => s.Params["reviewer_name"] == "Dr. Independent").Params["url"], Is.EqualTo("https://dd.example.com/contribute/review/other"));
            Assert.That(_context.HealthWikiContributors.Single(c => c.ContributorId == hospital.ContributorId).Mobile, Is.Null);
        }

        [Test]
        public async Task APublishedArticle_TellsTheAuthorAndTheReviewerOnce_EachWithTheirOwnName()
        {
            var author = Person("Priya Menon", mobile: "9891122003"); var reviewer = Person("Dr. Meera Nair");

            await Notifier().PublishedAsync(Article(), author.ContributorId, reviewer.ContributorId, Ct);
            Assert.That(_sent.Select(s => s.Params["person_name"]), Is.EquivalentTo(new[] { "Priya Menon", "Dr. Meera Nair" }));
            Assert.That(_sent.Select(s => s.Params["url"]).Distinct(), Is.EqualTo(new[] { "https://dd.example.com/health/conditions/high-blood-pressure" }));

            _sent.Clear();
            await Notifier().PublishedAsync(Article("same"), author.ContributorId, author.ContributorId, Ct);
            Assert.That(_sent, Has.Count.EqualTo(1), "someone who is both author and reviewer hears once");

            _sent.Clear();
            await Notifier().PublishedAsync(Article("noauthor"), null, reviewer.ContributorId, Ct);
            Assert.That(_sent, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task AnEditorsReturnIsMessageN9_ADoctorsIsN7_SameTemplate()
        {
            var author = Person("Priya Menon", mobile: "9891122003");
            await Notifier().ChangesRequestedAsync(Article("a"), author.ContributorId, "the NexEagle editorial team", "More sources.", true, Ct);
            await Notifier().ChangesRequestedAsync(Article("b"), author.ContributorId, "Dr. Meera Nair", "More sources.", false, Ct);

            Assert.That(_sent.Select(s => s.Template).Distinct(), Is.EqualTo(new[] { HealthWikiConstants.Template.ChangesRequested }));
            Assert.That(_context.HealthWikiNotifications.Select(n => n.Code).OrderBy(c => c), Is.EqualTo(new[] { "N7", "N9" }));
        }

        // ------------------------------------------------------------------------------------------ the record

        [Test]
        public async Task ASentMessage_IsRecordedWithOnlyAMaskedNumber_AndShowsInTheArticlesHistory()
        {
            var asha = Person(); var a = Article();
            await Notifier().ReviewRequestedAsync(a, asha.ContributorId, Ct);

            var row = _context.HealthWikiNotifications.Single();
            Assert.That(row.Code, Is.EqualTo("N5"));
            Assert.That(row.TemplateName, Is.EqualTo(HealthWikiConstants.Template.ReviewRequest));
            Assert.That(row.Status, Is.EqualTo("SENT"));
            Assert.That(row.RecipientContributorId, Is.EqualTo(asha.ContributorId));
            Assert.That(row.RecipientMobileMasked, Is.EqualTo("+91 98••• ••002"));
            Assert.That(row.RecipientMobileMasked, Does.Not.Contain("9891122002"));
            var audit = _context.HealthWikiAudits.Single();
            Assert.That(audit.EntityType, Is.EqualTo("ARTICLE"));
            Assert.That(audit.EntityId, Is.EqualTo(a.ArticleId));
            Assert.That(HealthWikiAuditLog.Label(audit.Action), Is.EqualTo("Told on WhatsApp"));
        }

        [Test]
        public async Task WhenWhatsAppRefuses_ItIsRecordedAsFailed_NothingThrows_AndTheReminderSaysNotSent()
        {
            _accept = false;
            var asha = Person();

            var sent = await Notifier().ReviewReminderAsync(Article(), asha.ContributorId, 3, Ct);

            Assert.That(sent, Is.False);
            var row = _context.HealthWikiNotifications.Single();
            Assert.That(row.Status, Is.EqualTo("FAILED"));
            Assert.That(row.Detail, Does.Contain("template may not be approved"));
            Assert.That(HealthWikiAuditLog.Label(_context.HealthWikiAudits.Single().Action), Is.EqualTo("Could not tell on WhatsApp"));
        }

        [Test]
        public async Task WhenTheWhatsAppCallItselfBreaks_ItIsStillRecorded_AndNothingEscapes()
        {
            _throws = new InvalidOperationException("boom: connection reset");
            var asha = Person();

            Assert.DoesNotThrowAsync(() => Notifier().ProfileApprovedAsync(asha, Ct));

            var row = _context.HealthWikiNotifications.Single();
            Assert.That(row.Status, Is.EqualTo("FAILED"));
            Assert.That(row.Detail, Is.EqualTo("WhatsApp could not be reached."));
            Assert.That(row.Detail, Does.Not.Contain("boom"));
        }

        [Test]
        public async Task NoNumber_NoSend_ItIsRecordedAsSkipped()
        {
            var nobody = Person("No Number", mobile: null);

            await Notifier().ProfileApprovedAsync(nobody, Ct);
            await Notifier().ReviewRequestedAsync(Article(), Guid.NewGuid(), Ct);

            Assert.That(_sent, Is.Empty);
            Assert.That(_context.HealthWikiNotifications.Select(n => n.Status), Is.All.EqualTo("SKIPPED"));
            Assert.That(_context.HealthWikiNotifications.Select(n => n.Detail), Has.Some.Contains("no mobile number"));
            Assert.That(_context.HealthWikiNotifications.Select(n => n.Detail), Has.Some.Contains("no one to send"));
        }

        [Test]
        public async Task WithoutTheSiteAddress_AMessageThatNeedsALinkIsSkipped_ButOneWithoutALinkStillGoes()
        {
            var asha = Person();

            await Notifier(doctorDekho: "<set-via-env-HealthWiki-ContributorBaseUrl>").ProfileApprovedAsync(asha, Ct);
            await Notifier(doctorDekho: "<set-via-env-HealthWiki-ContributorBaseUrl>").RegistrationReceivedAsync(asha, Ct);

            Assert.That(_sent.Select(s => s.Template), Is.EqualTo(new[] { HealthWikiConstants.Template.RegistrationReceived }));
            Assert.That(_context.HealthWikiNotifications.Single(n => n.Status == "SKIPPED").Detail, Does.Contain("address is not configured"));
        }

        [Test]
        public async Task TheSameMessageTwiceInAMoment_IsOneMessage()
        {
            var asha = Person(); var a = Article();
            await Notifier().ReviewRequestedAsync(a, asha.ContributorId, Ct);
            await Notifier().ReviewRequestedAsync(a, asha.ContributorId, Ct);

            Assert.That(_sent, Has.Count.EqualTo(1));
            Assert.That(_context.HealthWikiNotifications.Count(), Is.EqualTo(1));
        }

        [Test]
        public async Task ALongCommentWithLineBreaks_ReachesWhatsAppAsOneShortLine()
        {
            // The WhatsApp service flattens and cuts values; the notifier hands it the raw text and records nothing sensitive.
            var author = Person("Priya Menon", mobile: "9891122003");
            await Notifier().ChangesRequestedAsync(Article(), author.ContributorId, "Dr. Meera Nair", "Line one\nLine two\t" + new string('x', 2000), false, Ct);
            Assert.That(_sent.Single().Params["comment"], Does.StartWith("Line one"));
            Assert.That(_context.HealthWikiNotifications.Single().Detail, Is.Null);
        }

        // ------------------------------------------------------------------------------------------ the addresses

        [Test]
        public void TheLinksAreExact_AndASlugCannotChangeThePath()
        {
            Assert.That(HealthWikiLinks.ContributorHome("https://dd.example.com/"), Is.EqualTo("https://dd.example.com/contribute"));
            Assert.That(HealthWikiLinks.Write("https://dd.example.com", "a-b"), Is.EqualTo("https://dd.example.com/contribute/write/a-b"));
            Assert.That(HealthWikiLinks.Review("https://dd.example.com", "a-b"), Is.EqualTo("https://dd.example.com/contribute/review/a-b"));
            Assert.That(HealthWikiLinks.Topics("https://dd.example.com"), Is.EqualTo("https://dd.example.com/contribute/topics"));
            Assert.That(HealthWikiLinks.PublicArticle("https://dd.example.com", "a-b"), Is.EqualTo("https://dd.example.com/health/conditions/a-b"));
            Assert.That(HealthWikiLinks.EasyHmsReview("https://1hms.example.com/", "a-b"), Is.EqualTo("https://1hms.example.com/health-wiki/a-b"));
            Assert.That(HealthWikiLinks.Write("https://dd.example.com", "../admin"), Is.EqualTo("https://dd.example.com/contribute/write/..%2Fadmin"));
            Assert.That(HealthWikiLinks.Write(null, "a"), Is.Null);
            Assert.That(HealthWikiLinks.Write("  ", "a"), Is.Null);
        }
    }
}
