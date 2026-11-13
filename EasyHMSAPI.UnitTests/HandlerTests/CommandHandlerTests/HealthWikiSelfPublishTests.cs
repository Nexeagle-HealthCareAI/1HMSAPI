using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Application.Services.Implementations;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    /// <summary>A verified doctor publishes their own articles with no review; the team can pull one back to review or take it offline.</summary>
    [TestFixture]
    public class HealthWikiSelfPublishTests
    {
        private AppDbContext _context = null!;
        private RecordingNotifier _notifier = null!;
        private CancellationToken Ct => CancellationToken.None;
        private long _mobile = 9100000100;

        [SetUp]
        public void SetUp() { _context = InMemoryDbContextFactory.CreateContext(); _notifier = new RecordingNotifier(); }

        [TearDown]
        public void TearDown() { InMemoryDbContextFactory.Destroy(_context); _context.Dispose(); }

        private HealthWikiContributor Person(string type = "INDEPENDENT_DOCTOR", string status = "VERIFIED", string name = "Dr. Meera Nair")
        {
            var c = new HealthWikiContributor
            {
                ContributorId = Guid.NewGuid(), Type = type, FullName = name, Mobile = (_mobile++).ToString(), Status = status,
                Qualification = "MBBS", Speciality = "Physician", RegistrationNumber = "1", RegistrationCouncil = "Delhi Medical Council",
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            _context.HealthWikiContributors.Add(c);
            _context.SaveChanges();
            return c;
        }

        private async Task<string> NewDraft(HealthWikiContributor author, string type = "MEDICAL", string title = "Living with High Blood Pressure", string? slug = null)
        {
            var res = await new SaveContributorArticleHandler(_context, new HealthArticleService(_context, _notifier)).Handle(new SaveContributorArticleRequestModel
            { ContributorId = author.ContributorId, Slug = slug, Type = type, Title = title, Description = "Habits that help.", Content = "## Know your numbers" }, Ct);
            Assert.That(res.Success, Is.True, res.Message);
            return res.Article!.Slug;
        }

        private Task<EasyHMSAPI.Application.ResponseModels.CommandResponseModels.ContributorArticleResponseModel> Submit(HealthWikiContributor who, string slug) =>
            new SubmitContributorArticleHandler(_context, _notifier).Handle(new SubmitContributorArticleRequestModel { SubmitConsent = true, ContributorId = who.ContributorId, Slug = slug }, Ct);

        private Task<EasyHMSAPI.Application.ResponseModels.CommandResponseModels.SaveHealthArticleResponseModel> Decide(string slug, string action, string? reason = "because") =>
            new DecideHealthArticleHandler(_context, _notifier).Handle(new DecideHealthArticleRequestModel { Slug = slug, Action = action, Reason = reason, ActorName = "Editor" }, Ct);

        [TestCase("INDEPENDENT_DOCTOR", "MEDICAL")]
        [TestCase("HOSPITAL_DOCTOR", "MEDICAL")]
        [TestCase("INDEPENDENT_DOCTOR", "SECTOR_UPDATE")]
        public async Task AVerifiedDoctor_PublishesTheirOwnArticle_WithoutReview(string type, string articleType)
        {
            var doctor = Person(type);
            var slug = await NewDraft(doctor, articleType);

            var res = await Submit(doctor, slug);

            Assert.That(res.Success, Is.True, res.Message);
            var a = _context.HealthArticles.Single();
            Assert.That(a.Status, Is.EqualTo("PUBLISHED"));
            Assert.That(a.PublishedAt, Is.Not.Null);
            Assert.That(a.ApprovedAt, Is.Not.Null);
            Assert.That(a.ApprovedByName, Is.EqualTo(doctor.FullName));
            Assert.That(a.ReviewerContributorId, Is.Null, "nobody reviewed it, so no 'Medically reviewed by' badge");
            Assert.That(_context.HealthWikiAudits.Any(x => x.EntityId == a.ArticleId && x.Action == "SELF_PUBLISHED"), Is.True);
        }

        [Test]
        public async Task AReviewerAssignedBeforeIsDropped_SoTheBadgeCannotClaimAReviewNobodyDid()
        {
            var doctor = Person(); var other = Person(name: "Dr. Other");
            var slug = await NewDraft(doctor);
            _context.HealthArticles.Single().ReviewerContributorId = other.ContributorId;
            await _context.SaveChangesAsync();

            await Submit(doctor, slug);

            Assert.That(_context.HealthArticles.Single().ReviewerContributorId, Is.Null);
        }

        [TestCase("PENDING")]
        [TestCase("INVITED")]
        public async Task ADoctorWhoIsNotVerifiedYet_StillGoesThroughReview(string status)
        {
            var doctor = Person(status: status == "INVITED" ? "PENDING" : status);
            var slug = await NewDraft(doctor, "SECTOR_UPDATE");

            await Submit(doctor, slug);

            Assert.That(_context.HealthArticles.Single().Status, Is.EqualTo("IN_REVIEW"));
        }

        [Test]
        public async Task AWriterOrHealthWorker_StillGoesThroughReview()
        {
            var writer = Person("WRITER");
            var slug = await NewDraft(writer, "SECTOR_UPDATE");

            await Submit(writer, slug);

            Assert.That(_context.HealthArticles.Single().Status, Is.EqualTo("IN_REVIEW"));
        }

        [Test]
        public async Task EditingALiveArticle_GoesLiveAtOnce_AndTheEditStaysInTheHistory()
        {
            var doctor = Person();
            var slug = await NewDraft(doctor);
            await Submit(doctor, slug);

            await NewDraft(doctor, "MEDICAL", "High Blood Pressure, updated", slug);
            var res = await Submit(doctor, slug);

            Assert.That(res.Success, Is.True, res.Message);
            var a = _context.HealthArticles.Single();
            Assert.That(a.Title, Is.EqualTo("High Blood Pressure, updated"));
            Assert.That(res.Article!.HasPendingRevision, Is.False);
            Assert.That(_context.HealthArticleRevisions.Single().Status, Is.EqualTo(HealthArticleRevision.StatusApplied));
            var events = _context.HealthWikiAudits.Where(x => x.EntityId == a.ArticleId).Select(x => x.Action).ToList();
            Assert.That(events, Does.Contain("REVISION_APPLIED"));
            Assert.That(events.Count(e => e == "SELF_PUBLISHED"), Is.EqualTo(2), "the first publication and the edit are both recorded");
        }

        [Test]
        public async Task PullingALiveArticleBack_TakesItOffline_PutsItInReview_AndTheAuthorCannotRepublishAlone()
        {
            var doctor = Person();
            var slug = await NewDraft(doctor, "SECTOR_UPDATE");
            await Submit(doctor, slug);

            var pulled = await Decide(slug, DecideHealthArticleRequestModel.PullBack, "Please check the dose in paragraph 3");

            Assert.That(pulled.Success, Is.True, pulled.Message);
            var a = _context.HealthArticles.Single();
            Assert.That(a.Status, Is.EqualTo("IN_REVIEW"));
            Assert.That(a.PublishedAt, Is.Null);
            Assert.That(a.ApprovedAt, Is.Null);
            Assert.That(a.HoldForReview, Is.True);
            Assert.That(_context.HealthWikiAudits.Any(x => x.EntityId == a.ArticleId && x.Action == "PULLED_BACK"), Is.True);

            // sent back to the author for changes: they fix it, but may not publish it alone any more
            await Decide(slug, DecideHealthArticleRequestModel.Withdraw, "Fix the dose");
            await Submit(doctor, slug);
            Assert.That(_context.HealthArticles.Single().Status, Is.EqualTo("IN_REVIEW"));

            // the editor approves: it is live again and the hold is lifted
            var approved = await Decide(slug, DecideHealthArticleRequestModel.Approve, null);
            Assert.That(approved.Success, Is.True, approved.Message);
            a = _context.HealthArticles.Single();
            Assert.That(a.Status, Is.EqualTo("PUBLISHED"));
            Assert.That(a.HoldForReview, Is.False);

            // and the doctor can publish their next edit alone again
            await NewDraft(doctor, "SECTOR_UPDATE", "Edited again", slug);
            await Submit(doctor, slug);
            Assert.That(_context.HealthArticles.Single().Title, Is.EqualTo("Edited again"));
            Assert.That(_context.HealthArticles.Single().Status, Is.EqualTo("PUBLISHED"));
        }

        [Test]
        public async Task PullBack_NeedsAReason_AndALiveArticle()
        {
            var doctor = Person();
            var slug = await NewDraft(doctor);

            Assert.That((await Decide(slug, DecideHealthArticleRequestModel.PullBack, "x")).StatusCode, Is.EqualTo(409), "a draft is not live");
            await Submit(doctor, slug);
            Assert.That((await Decide(slug, DecideHealthArticleRequestModel.PullBack, " ")).StatusCode, Is.EqualTo(400));
            Assert.That(_context.HealthArticles.Single().Status, Is.EqualTo("PUBLISHED"));
        }

        [Test]
        public async Task PullBackOfAnEditInProgress_KeepsThatText_AndTheAuthorIsTold()
        {
            var doctor = Person();
            var slug = await NewDraft(doctor);
            await Submit(doctor, slug);
            await NewDraft(doctor, "MEDICAL", "Half-written edit", slug);

            await Decide(slug, DecideHealthArticleRequestModel.PullBack, "check it");

            var a = _context.HealthArticles.Single();
            Assert.That(a.Title, Is.EqualTo("Half-written edit"));
            Assert.That(_context.HealthArticleRevisions.Single().Status, Is.EqualTo(HealthArticleRevision.StatusApplied));
        }

        [Test]
        public async Task PullUp_PutsAPulledBackArticleBackLive_WithNoReview_AndTheHistoryNamesTheEditor()
        {
            var doctor = Person();
            var slug = await NewDraft(doctor);
            await Submit(doctor, slug);
            await Decide(slug, DecideHealthArticleRequestModel.PullBack, "Checking a claim");
            Assert.That(_context.HealthArticles.Single().Status, Is.EqualTo("IN_REVIEW"));

            Assert.That((await Decide(slug, DecideHealthArticleRequestModel.PullUp, " ")).StatusCode, Is.EqualTo(400), "a reason is needed");
            var up = await Decide(slug, DecideHealthArticleRequestModel.PullUp, "Claim checked, all fine");

            Assert.That(up.Success, Is.True, up.Message);
            var a = _context.HealthArticles.Single();
            Assert.That(a.Status, Is.EqualTo("PUBLISHED"));
            Assert.That(a.HoldForReview, Is.False);
            Assert.That(a.ApprovedByName, Is.EqualTo("Editor"));
            Assert.That(_context.HealthWikiAudits.Any(x => x.EntityId == a.ArticleId && x.Action == "PUT_BACK_LIVE" && x.Detail == "Claim checked, all fine"), Is.True);
        }

        [Test]
        public async Task PullUp_AlsoBringsBackAnArticleTheTeamTookDown_ButNotOneThatIsAlreadyLive()
        {
            var doctor = Person();
            var slug = await NewDraft(doctor);
            await Submit(doctor, slug);
            Assert.That((await Decide(slug, DecideHealthArticleRequestModel.PullUp, "x")).StatusCode, Is.EqualTo(409), "already live");

            await Decide(slug, DecideHealthArticleRequestModel.Unpublish, "Wrong");
            Assert.That(_context.HealthArticles.Single().HoldForReview, Is.True);
            await Submit(doctor, slug);
            Assert.That(_context.HealthArticles.Single().Status, Is.EqualTo("IN_REVIEW"), "the doctor cannot put it back alone");
            Assert.That((await Decide(slug, DecideHealthArticleRequestModel.PullUp, "Fixed")).Success, Is.True);
            Assert.That(_context.HealthArticles.Single().Status, Is.EqualTo("PUBLISHED"));
        }

        [Test]
        public async Task APulledBackEditInProgress_DropsTheReviewerWhoNeverSawIt()
        {
            var author = Person(status: "PENDING"); var reviewer = Person(name: "Dr. Reviewer");
            var slug = await NewDraft(author);
            var art = _context.HealthArticles.Single(); art.ReviewerContributorId = reviewer.ContributorId; await _context.SaveChangesAsync();
            await Submit(author, slug);
            art = _context.HealthArticles.Single(); art.Status = "PUBLISHED"; art.ApprovedAt = DateTime.UtcNow; art.PublishedAt = DateTime.UtcNow; await _context.SaveChangesAsync();
            await NewDraft(author, "MEDICAL", "Edited text", slug);

            await Decide(slug, DecideHealthArticleRequestModel.PullBack, "check");

            Assert.That(_context.HealthArticles.Single().ReviewerContributorId, Is.Null);
        }

        [Test]
        public async Task ADoctorsTopic_IsWrittenByTheTeam_AndReviewedByThatDoctor()
        {
            var doctor = Person(); var worker = Person("HEALTH_WORKER", name: "Asha");
            var svc = new HealthWikiTopicService(_context);
            var mine = await svc.CreateAsync(new CreateContributorTopicRequestModel { ContributorId = doctor.ContributorId, Title = "Heat stroke", Outline = "o", WhyItMatters = "w", Type = "MEDICAL", RequestWriter = false }, Ct);
            var theirs = await svc.CreateAsync(new CreateContributorTopicRequestModel { ContributorId = worker.ContributorId, Title = "Camps", Outline = "o", WhyItMatters = "w", Type = "SECTOR_UPDATE" }, Ct);

            Assert.That(mine.Topic!.RequestWriter, Is.True, "a doctor's topic always goes to the team");
            Assert.That(theirs.Topic!.RequestWriter, Is.False, "a health worker writes their own");

            var accept = await new DecideTopicHandler(_context, _notifier).Handle(new DecideTopicRequestModel { TopicId = mine.Topic.Id, Action = DecideTopicRequestModel.Accept, ContentTeam = true, ActorName = "Editor" }, Ct);
            Assert.That(accept.Success, Is.True, accept.Message);
            var art = _context.HealthArticles.Single();
            Assert.That(art.AuthorContributorId, Is.Null, "the content team writes it");
            Assert.That(art.ReviewerContributorId, Is.EqualTo(doctor.ContributorId), "the doctor who asked reviews it");
        }

        [Test]
        public async Task PullUp_OfATeamWrittenMedicalArticleWithNoReviewer_AsksForOneInsteadOfFailingInTheDatabase()
        {
            var art = new HealthArticle { ArticleId = Guid.NewGuid(), Slug = "team-written", Type = "MEDICAL", Title = "T", Content = "c", Status = "IN_REVIEW", HoldForReview = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            _context.HealthArticles.Add(art);
            await _context.SaveChangesAsync();

            var res = await Decide("team-written", DecideHealthArticleRequestModel.PullUp, "ok");

            Assert.That(res.StatusCode, Is.EqualTo(400));
            Assert.That(res.Message, Does.Contain("reviewer"));
            Assert.That(_context.HealthArticles.Single().Status, Is.EqualTo("IN_REVIEW"));
        }

        [Test]
        public async Task Unpublish_StillWorksOnASelfPublishedArticle()
        {
            var doctor = Person();
            var slug = await NewDraft(doctor);
            await Submit(doctor, slug);

            var res = await Decide(slug, DecideHealthArticleRequestModel.Unpublish, "wrong");

            Assert.That(res.Success, Is.True, res.Message);
            Assert.That(_context.HealthArticles.Single().Status, Is.EqualTo("DRAFT"));
        }
    }
}
