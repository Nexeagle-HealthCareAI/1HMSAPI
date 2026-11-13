using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Application.Services.Implementations;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    /// <summary>The doctor sees what happened to their article, in their own words, and nothing that is the team's own business.</summary>
    [TestFixture]
    public class HealthWikiDoctorHistoryTests
    {
        private AppDbContext _context = null!;
        private RecordingNotifier _notifier = null!;
        private CancellationToken Ct => CancellationToken.None;
        private long _mobile = 9100000200;

        [SetUp]
        public void SetUp() { _context = InMemoryDbContextFactory.CreateContext(); _notifier = new RecordingNotifier(); }

        [TearDown]
        public void TearDown() { InMemoryDbContextFactory.Destroy(_context); _context.Dispose(); }

        private HealthWikiContributor Doctor(string name = "Dr. Meera Nair")
        {
            var c = new HealthWikiContributor
            {
                ContributorId = Guid.NewGuid(), Type = "INDEPENDENT_DOCTOR", FullName = name, Mobile = (_mobile++).ToString(), Status = "VERIFIED",
                Qualification = "MBBS", Speciality = "Physician", RegistrationNumber = "1", RegistrationCouncil = "Delhi Medical Council",
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            _context.HealthWikiContributors.Add(c);
            _context.SaveChanges();
            return c;
        }

        private async Task<string> PublishedByDoctor(HealthWikiContributor doctor)
        {
            var save = await new SaveContributorArticleHandler(_context, new HealthArticleService(_context, _notifier)).Handle(new SaveContributorArticleRequestModel
            { ContributorId = doctor.ContributorId, Type = "MEDICAL", Title = "Living with High Blood Pressure", Description = "Habits that help.", Content = "## Know your numbers" }, Ct);
            var slug = save.Article!.Slug;
            await new SubmitContributorArticleHandler(_context, _notifier).Handle(new SubmitContributorArticleRequestModel { SubmitConsent = true, ContributorId = doctor.ContributorId, Slug = slug }, Ct);
            return slug;
        }

        private Task Decide(string slug, string action, string? reason) =>
            new DecideHealthArticleHandler(_context, _notifier).Handle(new DecideHealthArticleRequestModel { Slug = slug, Action = action, Reason = reason, ActorName = "Editor Asha" }, Ct);

        private Task<EasyHMSAPI.Application.ResponseModels.QueryResponseModels.GetContributorArticleHistoryResponseModel> History(HealthWikiContributor who, string slug) =>
            new GetContributorArticleHistoryHandler(_context).Handle(new GetContributorArticleHistoryRequestModel { ContributorId = who.ContributorId, Slug = slug }, Ct);

        [Test]
        public async Task TheDoctorSeesTheirOwnPublish_AndThePullBackAndPutBackFromTheTeam_NewestFirst()
        {
            var doctor = Doctor();
            var slug = await PublishedByDoctor(doctor);
            await Decide(slug, "PULL_BACK", "Please add the sources.");
            await Decide(slug, "PULL_UP", "Sources added by the team.");

            var res = await History(doctor, slug);

            Assert.That(res.Success, Is.True, res.Message);
            var kinds = res.Entries.Select(e => e.Kind).ToList();
            Assert.That(kinds, Is.EqualTo(new[] { "PUT_BACK_LIVE", "PULLED_BACK", "SELF_PUBLISHED", "CREATED" }));
            var putBack = res.Entries[0];
            Assert.That(putBack.Action, Is.EqualTo("Put back live"));
            Assert.That(putBack.Actor, Is.EqualTo("NexEagle team"), "a team member's own name is not shown to the doctor");
            Assert.That(putBack.Detail, Is.EqualTo("Sources added by the team."));
            Assert.That(res.Entries[1].Detail, Is.EqualTo("Please add the sources."));
            Assert.That(res.Entries[2].ByMe, Is.True);
            Assert.That(res.Entries[2].Detail, Is.Null, "internal wording is not shown");
        }

        [Test]
        public async Task WhatsAppDeliveryNotes_AreNotShownToTheDoctor()
        {
            var doctor = Doctor();
            var slug = await PublishedByDoctor(doctor);
            var id = _context.HealthArticles.Single().ArticleId;
            HealthWikiAuditLog.Add(_context, HealthWikiAudit.EntityArticle, id, "NOTIFIED", HealthWikiAudit.ActorSystem, null, "N5 sent");
            HealthWikiAuditLog.Add(_context, HealthWikiAudit.EntityArticle, id, "NOTIFY_FAILED", HealthWikiAudit.ActorSystem, null, "template missing");
            await _context.SaveChangesAsync();

            var res = await History(doctor, slug);

            Assert.That(res.Entries.Select(e => e.Kind), Has.None.StartsWith("NOTIFY"));
        }

        [Test]
        public async Task AnotherDoctor_CannotReadSomeoneElsesHistory()
        {
            var author = Doctor(); var other = Doctor("Dr. Other");
            var slug = await PublishedByDoctor(author);

            var res = await History(other, slug);

            Assert.That(res.Success, Is.False);
            Assert.That(res.StatusCode, Is.EqualTo(404));
        }
    }
}
