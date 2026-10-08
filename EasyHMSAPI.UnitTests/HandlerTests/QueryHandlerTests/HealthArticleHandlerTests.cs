using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class HealthArticleHandlerTests
    {
        private AppDbContext _context = null!;
        private SaveHealthArticleHandler _save = null!;
        private DecideHealthArticleHandler _decide = null!;
        private GetPublicHealthArticlesHandler _get = null!;
        private GetHealthArticlesAdminHandler _admin = null!;
        private GetHealthArticleHistoryHandler _history = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _save = new SaveHealthArticleHandler(_context);
            _decide = new DecideHealthArticleHandler(_context);
            _get = new GetPublicHealthArticlesHandler(_context);
            _admin = new GetHealthArticlesAdminHandler(_context);
            _history = new GetHealthArticleHistoryHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private HealthWikiContributor AddContributor(string type, string status = HealthWikiContributor.StatusVerified, string name = "Dr. Meera Nair")
        {
            var c = new HealthWikiContributor
            {
                ContributorId = Guid.NewGuid(), Type = type, FullName = name, Status = status,
                Qualification = "MBBS, MD", Speciality = "Endocrinologist", RegistrationNumber = "48213", RegistrationCouncil = "Delhi Medical Council",
                Mobile = "9891122002", Email = "private@example.com", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            _context.HealthWikiContributors.Add(c);
            _context.SaveChanges();
            return c;
        }

        private static SaveHealthArticleRequestModel NewArticle(string slug = "diabetes-type-2", string? status = null, string? type = null) => new()
        {
            IsCreate = true, Slug = slug, Title = "Understanding Type 2 Diabetes",
            Description = "Guide", Content = "## What is it?", Status = status, Type = type,
        };

        /// <summary>A published SECTOR_UPDATE (the CMS editor publishes these directly).</summary>
        private async Task<string> PublishedSector(string slug = "telemedicine-rules")
        {
            var res = await _save.Handle(NewArticle(slug, "PUBLISHED", "SECTOR_UPDATE"), CancellationToken.None);
            Assert.That(res.Success, Is.True, res.Message);
            return slug;
        }

        // ------------------------------------------------------------------------------------------ rules

        [TestCase("diabetes-type-2", true)]
        [TestCase("a", true)]
        [TestCase("Diabetes", false)]
        [TestCase("two--hyphens", false)]
        [TestCase("-leading", false)]
        [TestCase("has space", false)]
        [TestCase("", false)]
        public void IsValidSlug_ChecksFormat(string slug, bool expected) =>
            Assert.That(HealthArticleRules.IsValidSlug(slug), Is.EqualTo(expected));

        [TestCase("published", "PUBLISHED")]
        [TestCase("in-review", "IN_REVIEW")]
        [TestCase("In Review", "IN_REVIEW")]
        [TestCase("draft", "DRAFT")]
        [TestCase("archived", null)]
        public void NormalizeStatus_MapsKnownValues(string input, string? expected) =>
            Assert.That(HealthArticleRules.NormalizeStatus(input), Is.EqualTo(expected));

        [TestCase("medical", "MEDICAL")]
        [TestCase("sector update", "SECTOR_UPDATE")]
        [TestCase("news", null)]
        public void NormalizeType_MapsKnownValues(string input, string? expected) =>
            Assert.That(HealthArticleRules.NormalizeType(input), Is.EqualTo(expected));

        [TestCase("MEDICAL", "INDEPENDENT_DOCTOR", true)]
        [TestCase("MEDICAL", "HOSPITAL_DOCTOR", true)]
        [TestCase("MEDICAL", "STAFF", true)]
        [TestCase("MEDICAL", "HEALTH_WORKER", false)]
        [TestCase("MEDICAL", "WRITER", false)]
        [TestCase("SECTOR_UPDATE", "WRITER", true)]
        [TestCase("SECTOR_UPDATE", "HEALTH_WORKER", true)]
        public void CanAuthor_HealthWorkersAndWritersCannotWriteMedical(string articleType, string contributorType, bool expected) =>
            Assert.That(HealthArticleRules.CanAuthor(articleType, contributorType), Is.EqualTo(expected));

        [TestCase("MEDICAL", "INDEPENDENT_DOCTOR", "VERIFIED", true)]
        [TestCase("MEDICAL", "INDEPENDENT_DOCTOR", "PENDING", false)]
        [TestCase("MEDICAL", "HEALTH_WORKER", "VERIFIED", false)]
        [TestCase("SECTOR_UPDATE", "INDEPENDENT_DOCTOR", "VERIFIED", false)]
        public void ShowsReviewerBadge_OnlyVerifiedDoctorOnMedical(string articleType, string reviewerType, string status, bool expected) =>
            Assert.That(HealthArticleRules.ShowsReviewerBadge(articleType, reviewerType, status), Is.EqualTo(expected));

        [TestCase("https://cdn.example.com/a.jpg", true)]
        [TestCase("http://cdn.example.com/a.jpg", false)]
        [TestCase("javascript:alert(1)", false)]
        [TestCase("/relative.jpg", false)]
        public void IsHttpsUrl_AcceptsOnlyHttps(string url, bool expected) =>
            Assert.That(HealthArticleRules.IsHttpsUrl(url), Is.EqualTo(expected));

        // ------------------------------------------------------------------------------------------ create

        [Test]
        public async Task Create_DefaultsToMedicalDraft_AndPublicApiHidesIt()
        {
            var res = await _save.Handle(NewArticle(), CancellationToken.None);

            Assert.That(res.Success, Is.True);
            Assert.That(res.StatusCode, Is.EqualTo(201));
            Assert.That(res.Status, Is.EqualTo("DRAFT"));
            Assert.That(res.PublishedAt, Is.Null);
            Assert.That(res.Article!.Type, Is.EqualTo("MEDICAL"));
            var list = await _get.Handle(new GetPublicHealthArticlesRequestModel(), CancellationToken.None);
            Assert.That(list.Articles, Is.Empty);
        }

        [Test]
        public async Task Create_DuplicateSlug_Returns409()
        {
            await _save.Handle(NewArticle(), CancellationToken.None);
            var res = await _save.Handle(NewArticle(), CancellationToken.None);

            Assert.That(res.StatusCode, Is.EqualTo(409));
        }

        [Test]
        public async Task Create_InvalidInput_Returns400()
        {
            var badSlug = await _save.Handle(NewArticle("Bad Slug"), CancellationToken.None);
            var badStatus = await _save.Handle(NewArticle(status: "archived"), CancellationToken.None);
            var badType = await _save.Handle(NewArticle(type: "news"), CancellationToken.None);
            var noContent = await _save.Handle(new SaveHealthArticleRequestModel { IsCreate = true, Slug = "x", Title = "t" }, CancellationToken.None);

            Assert.That(new[] { badSlug.StatusCode, badStatus.StatusCode, badType.StatusCode, noContent.StatusCode }, Is.All.EqualTo(400));
        }

        [Test]
        public async Task Create_CoverNeedsHttpsAndAltText()
        {
            var noAlt = NewArticle("a"); noAlt.CoverImageUrl = "https://cdn.example.com/a.jpg";
            var http = NewArticle("b"); http.CoverImageUrl = "http://cdn.example.com/a.jpg"; http.CoverImageAlt = "x";
            var ok = NewArticle("c"); ok.CoverImageUrl = "https://cdn.example.com/a.jpg"; ok.CoverImageAlt = "A blood pressure monitor";

            Assert.That((await _save.Handle(noAlt, CancellationToken.None)).StatusCode, Is.EqualTo(400));
            Assert.That((await _save.Handle(http, CancellationToken.None)).StatusCode, Is.EqualTo(400));
            Assert.That((await _save.Handle(ok, CancellationToken.None)).StatusCode, Is.EqualTo(201));
        }

        [Test]
        public async Task Create_UnknownContributor_Returns400()
        {
            var req = NewArticle(); req.AuthorContributorId = Guid.NewGuid();
            Assert.That((await _save.Handle(req, CancellationToken.None)).StatusCode, Is.EqualTo(400));
        }

        [Test]
        public async Task Create_WriterCannotAuthorMedical_ButCanAuthorSectorUpdate()
        {
            var writer = AddContributor("WRITER", name: "A. Writer");
            var medical = NewArticle("m"); medical.AuthorContributorId = writer.ContributorId;
            var sector = NewArticle("s", type: "SECTOR_UPDATE"); sector.AuthorContributorId = writer.ContributorId;

            Assert.That((await _save.Handle(medical, CancellationToken.None)).StatusCode, Is.EqualTo(400));
            Assert.That((await _save.Handle(sector, CancellationToken.None)).StatusCode, Is.EqualTo(201));
        }

        [Test]
        public async Task Create_ReviewerMustBeADoctor_AndSectorUpdateHasNone()
        {
            var worker = AddContributor("HEALTH_WORKER", name: "H. Worker");
            var doctor = AddContributor("INDEPENDENT_DOCTOR");
            var notDoctor = NewArticle("a"); notDoctor.ReviewerContributorId = worker.ContributorId;
            var sectorWithReviewer = NewArticle("b", type: "SECTOR_UPDATE"); sectorWithReviewer.ReviewerContributorId = doctor.ContributorId;
            var ok = NewArticle("c"); ok.ReviewerContributorId = doctor.ContributorId;

            Assert.That((await _save.Handle(notDoctor, CancellationToken.None)).StatusCode, Is.EqualTo(400));
            Assert.That((await _save.Handle(sectorWithReviewer, CancellationToken.None)).StatusCode, Is.EqualTo(400));
            Assert.That((await _save.Handle(ok, CancellationToken.None)).StatusCode, Is.EqualTo(201));
        }

        // ------------------------------------------------------------------------------------------ publishing rules

        [Test]
        public async Task Medical_CannotBePublishedBySaving_EvenWithAVerifiedReviewer()
        {
            var doctor = AddContributor("INDEPENDENT_DOCTOR");
            var req = NewArticle(status: "PUBLISHED"); req.ReviewerContributorId = doctor.ContributorId;

            var res = await _save.Handle(req, CancellationToken.None);

            Assert.That(res.StatusCode, Is.EqualTo(400));
            Assert.That(res.Message, Does.Contain("reviewer approves"));
        }

        [Test]
        public async Task Medical_NeedsAReviewerBeforeItGoesForReview()
        {
            var res = await _save.Handle(NewArticle(status: "IN_REVIEW"), CancellationToken.None);
            Assert.That(res.StatusCode, Is.EqualTo(400));

            var doctor = AddContributor("INDEPENDENT_DOCTOR");
            var req = NewArticle("with-reviewer", "IN_REVIEW"); req.ReviewerContributorId = doctor.ContributorId;
            var ok = await _save.Handle(req, CancellationToken.None);
            Assert.That(ok.Status, Is.EqualTo("IN_REVIEW"));
            Assert.That(_context.HealthArticles.Single(a => a.Slug == "with-reviewer").SubmittedAt, Is.Not.Null);
        }

        [Test]
        public async Task Medical_PublishesOnceTheReviewerHasApproved_AndShowsTheBadge()
        {
            var doctor = AddContributor("INDEPENDENT_DOCTOR");
            var req = NewArticle(status: "IN_REVIEW"); req.ReviewerContributorId = doctor.ContributorId;
            await _save.Handle(req, CancellationToken.None);
            // What the reviewer endpoint will do when the doctor approves.
            _context.HealthArticles.Single().ApprovedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var res = await _save.Handle(new SaveHealthArticleRequestModel { Slug = "diabetes-type-2", Status = "PUBLISHED" }, CancellationToken.None);
            var list = await _get.Handle(new GetPublicHealthArticlesRequestModel(), CancellationToken.None);

            Assert.That(res.Success, Is.True, res.Message);
            Assert.That(list.Articles[0].Reviewer!.FullName, Is.EqualTo("Dr. Meera Nair"));
            Assert.That(list.Articles[0].Reviewer!.IsRegistrationVerified, Is.True);
        }

        [Test]
        public async Task Medical_UnverifiedReviewer_CannotPublish()
        {
            var doctor = AddContributor("INDEPENDENT_DOCTOR", status: "PENDING");
            var req = NewArticle(status: "IN_REVIEW"); req.ReviewerContributorId = doctor.ContributorId;
            await _save.Handle(req, CancellationToken.None);
            _context.HealthArticles.Single().ApprovedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var res = await _save.Handle(new SaveHealthArticleRequestModel { Slug = "diabetes-type-2", Status = "PUBLISHED" }, CancellationToken.None);

            Assert.That(res.StatusCode, Is.EqualTo(400));
        }

        [Test]
        public async Task SectorUpdate_PublishedByTheEditor_NeverShowsAReviewer()
        {
            var slug = await PublishedSector();
            var list = await _get.Handle(new GetPublicHealthArticlesRequestModel { Slug = slug }, CancellationToken.None);

            Assert.That(list.Articles, Has.Count.EqualTo(1));
            Assert.That(list.Articles[0].Type, Is.EqualTo("SECTOR_UPDATE"));
            Assert.That(list.Articles[0].Reviewer, Is.Null);
            Assert.That(list.Articles[0].PublishedAt, Is.Not.Null);
            Assert.That(_context.HealthArticles.Single().ApprovedByName, Is.EqualTo("CMS"));
        }

        [Test]
        public async Task PublicApi_NeverExposesAReviewerOnASectorUpdate_EvenIfOneIsStored()
        {
            var doctor = AddContributor("INDEPENDENT_DOCTOR");
            var slug = await PublishedSector();
            _context.HealthArticles.Single().ReviewerContributorId = doctor.ContributorId; // bad data
            await _context.SaveChangesAsync();

            var list = await _get.Handle(new GetPublicHealthArticlesRequestModel { Slug = slug }, CancellationToken.None);

            Assert.That(list.Articles[0].Reviewer, Is.Null);
        }

        [Test]
        public async Task PublicApi_NeverReturnsMobileOrEmail()
        {
            var writer = AddContributor("WRITER", name: "A. Writer");
            var req = NewArticle("s", "PUBLISHED", "SECTOR_UPDATE"); req.AuthorContributorId = writer.ContributorId;
            await _save.Handle(req, CancellationToken.None);

            var json = System.Text.Json.JsonSerializer.Serialize(await _get.Handle(new GetPublicHealthArticlesRequestModel(), CancellationToken.None));

            Assert.That(json, Does.Not.Contain("9891122002"));
            Assert.That(json, Does.Not.Contain("private@example.com"));
            Assert.That(json, Does.Contain("A. Writer"));
        }

        // ------------------------------------------------------------------------------------------ editing

        [Test]
        public async Task Update_OnlyProvidedFieldsChange_AndNullClears()
        {
            var req = NewArticle(); req.CoverImageUrl = "https://cdn.example.com/a.jpg"; req.CoverImageAlt = "alt"; req.Disclosure = "Sponsored";
            await _save.Handle(req, CancellationToken.None);

            var res = await _save.Handle(new SaveHealthArticleRequestModel
            {
                Slug = "diabetes-type-2", Title = "New title",
                Provided = new System.Collections.Generic.HashSet<string> { "title", "disclosure", "coverImageUrl", "coverImageAlt" },
            }, CancellationToken.None);
            var a = _context.HealthArticles.Single();

            Assert.That(res.Success, Is.True, res.Message);
            Assert.That(a.Title, Is.EqualTo("New title"));
            Assert.That(a.Content, Is.EqualTo("## What is it?"), "content was not sent, so it is unchanged");
            Assert.That(a.Disclosure, Is.Null, "sent as null, so cleared");
            Assert.That(a.CoverImageUrl, Is.Null);
        }

        [Test]
        public async Task Update_UnknownSlug_Returns404()
        {
            var res = await _save.Handle(new SaveHealthArticleRequestModel { Slug = "nope", Title = "x" }, CancellationToken.None);
            Assert.That(res.StatusCode, Is.EqualTo(404));
        }

        [Test]
        public async Task EditingAPublishedArticle_KeepsTheLiveVersionOnline_UntilTheEditIsApproved()
        {
            var slug = await PublishedSector();

            var edit = await _save.Handle(new SaveHealthArticleRequestModel { Slug = slug, Title = "Telemedicine rules, updated" }, CancellationToken.None);
            var publicNow = await _get.Handle(new GetPublicHealthArticlesRequestModel { Slug = slug }, CancellationToken.None);

            Assert.That(edit.Success, Is.True, edit.Message);
            Assert.That(edit.Status, Is.EqualTo("PUBLISHED"));
            Assert.That(edit.Article!.HasPendingRevision, Is.True);
            Assert.That(edit.Article.Title, Is.EqualTo("Telemedicine rules, updated"), "the CMS sees the working copy");
            Assert.That(publicNow.Articles[0].Title, Is.EqualTo("Understanding Type 2 Diabetes"), "readers still see the live text");

            var approved = await _decide.Handle(new DecideHealthArticleRequestModel { Slug = slug, Action = "APPROVE" }, CancellationToken.None);
            var publicAfter = await _get.Handle(new GetPublicHealthArticlesRequestModel { Slug = slug }, CancellationToken.None);

            Assert.That(approved.Success, Is.True, approved.Message);
            Assert.That(approved.Article!.HasPendingRevision, Is.False);
            Assert.That(publicAfter.Articles[0].Title, Is.EqualTo("Telemedicine rules, updated"));
            Assert.That(_context.HealthArticleRevisions.Single().Status, Is.EqualTo("APPLIED"));
        }

        [Test]
        public async Task EditingAPublishedArticle_TwiceReusesTheSameOpenRevision()
        {
            var slug = await PublishedSector();
            await _save.Handle(new SaveHealthArticleRequestModel { Slug = slug, Title = "One" }, CancellationToken.None);
            await _save.Handle(new SaveHealthArticleRequestModel { Slug = slug, Description = "Two" }, CancellationToken.None);

            var rev = _context.HealthArticleRevisions.Single();
            Assert.That(rev.Title, Is.EqualTo("One"));
            Assert.That(rev.Description, Is.EqualTo("Two"));
        }

        [Test]
        public async Task PublishedArticle_CannotBeReturnedToDraftOrChangeType_BySaving()
        {
            var slug = await PublishedSector();

            var toDraft = await _save.Handle(new SaveHealthArticleRequestModel { Slug = slug, Status = "DRAFT" }, CancellationToken.None);
            var newType = await _save.Handle(new SaveHealthArticleRequestModel { Slug = slug, Type = "MEDICAL" }, CancellationToken.None);

            Assert.That(toDraft.StatusCode, Is.EqualTo(400));
            Assert.That(newType.StatusCode, Is.EqualTo(400));
        }

        [Test]
        public async Task SendingAPendingEditForReview_NeedsAnEditFirst()
        {
            var slug = await PublishedSector();
            var none = await _save.Handle(new SaveHealthArticleRequestModel { Slug = slug, Status = "IN_REVIEW" }, CancellationToken.None);
            Assert.That(none.StatusCode, Is.EqualTo(400));

            await _save.Handle(new SaveHealthArticleRequestModel { Slug = slug, Title = "Edited" }, CancellationToken.None);
            var sent = await _save.Handle(new SaveHealthArticleRequestModel { Slug = slug, Status = "IN_REVIEW" }, CancellationToken.None);

            Assert.That(sent.Success, Is.True, sent.Message);
            Assert.That(sent.Article!.RevisionStatus, Is.EqualTo("IN_REVIEW"));
            Assert.That(sent.Status, Is.EqualTo("PUBLISHED"));
        }

        // ------------------------------------------------------------------------------------------ decisions

        [Test]
        public async Task Approve_RefusesAMedicalArticle_ThatIsTheDoctorsDecision()
        {
            var doctor = AddContributor("INDEPENDENT_DOCTOR");
            var req = NewArticle(status: "IN_REVIEW"); req.ReviewerContributorId = doctor.ContributorId;
            await _save.Handle(req, CancellationToken.None);

            var res = await _decide.Handle(new DecideHealthArticleRequestModel { Slug = "diabetes-type-2", Action = "APPROVE" }, CancellationToken.None);

            Assert.That(res.StatusCode, Is.EqualTo(400));
        }

        [Test]
        public async Task Approve_PublishesASectorUpdateInReview()
        {
            await _save.Handle(NewArticle("s", "IN_REVIEW", "SECTOR_UPDATE"), CancellationToken.None);

            var res = await _decide.Handle(new DecideHealthArticleRequestModel { Slug = "s", Action = "APPROVE", ActorName = "Priya (editor)" }, CancellationToken.None);

            Assert.That(res.Status, Is.EqualTo("PUBLISHED"));
            Assert.That(res.PublishedAt, Is.Not.Null);
            Assert.That(_context.HealthArticles.Single().ApprovedByName, Is.EqualTo("Priya (editor)"));
        }

        [Test]
        public async Task Approve_AlreadyPublishedWithNothingPending_Returns409()
        {
            var slug = await PublishedSector();
            var res = await _decide.Handle(new DecideHealthArticleRequestModel { Slug = slug, Action = "APPROVE" }, CancellationToken.None);
            Assert.That(res.StatusCode, Is.EqualTo(409));
        }

        [Test]
        public async Task Withdraw_NeedsAReason_AndReturnsAnInReviewArticleToDraft()
        {
            await _save.Handle(NewArticle("s", "IN_REVIEW", "SECTOR_UPDATE"), CancellationToken.None);

            var noReason = await _decide.Handle(new DecideHealthArticleRequestModel { Slug = "s", Action = "WITHDRAW" }, CancellationToken.None);
            var ok = await _decide.Handle(new DecideHealthArticleRequestModel { Slug = "s", Action = "WITHDRAW", Reason = "Please add the source." }, CancellationToken.None);

            Assert.That(noReason.StatusCode, Is.EqualTo(400));
            Assert.That(ok.Status, Is.EqualTo("DRAFT"));
            Assert.That(ok.Article!.ReviewerComment, Is.EqualTo("Please add the source."));
        }

        [Test]
        public async Task Withdraw_OfAPendingEdit_DoesNotTouchTheLiveArticle()
        {
            var slug = await PublishedSector();
            await _save.Handle(new SaveHealthArticleRequestModel { Slug = slug, Title = "Edited" }, CancellationToken.None);
            await _save.Handle(new SaveHealthArticleRequestModel { Slug = slug, Status = "IN_REVIEW" }, CancellationToken.None);

            var res = await _decide.Handle(new DecideHealthArticleRequestModel { Slug = slug, Action = "WITHDRAW", Reason = "Needs a source." }, CancellationToken.None);
            var live = await _get.Handle(new GetPublicHealthArticlesRequestModel { Slug = slug }, CancellationToken.None);

            Assert.That(res.Status, Is.EqualTo("PUBLISHED"));
            Assert.That(res.Article!.RevisionStatus, Is.EqualTo("DRAFT"));
            Assert.That(res.Article.ReviewerComment, Is.EqualTo("Needs a source."));
            Assert.That(live.Articles, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task Unpublish_TakesItOfflineAtOnce_AndKeepsTheEditInProgress()
        {
            var slug = await PublishedSector();
            await _save.Handle(new SaveHealthArticleRequestModel { Slug = slug, Title = "Edited" }, CancellationToken.None);

            var noReason = await _decide.Handle(new DecideHealthArticleRequestModel { Slug = slug, Action = "UNPUBLISH" }, CancellationToken.None);
            var res = await _decide.Handle(new DecideHealthArticleRequestModel { Slug = slug, Action = "UNPUBLISH", Reason = "Wrong dosage in the text." }, CancellationToken.None);
            var live = await _get.Handle(new GetPublicHealthArticlesRequestModel(), CancellationToken.None);

            Assert.That(noReason.StatusCode, Is.EqualTo(400));
            Assert.That(res.Status, Is.EqualTo("DRAFT"));
            Assert.That(live.Articles, Is.Empty);
            Assert.That(res.Article!.Title, Is.EqualTo("Edited"), "the edit became the draft text");
            Assert.That(_context.HealthArticles.Single().ApprovedAt, Is.Null);
        }

        [Test]
        public async Task Decide_UnknownArticleOrAction()
        {
            Assert.That((await _decide.Handle(new DecideHealthArticleRequestModel { Slug = "nope", Action = "APPROVE" }, CancellationToken.None)).StatusCode, Is.EqualTo(404));
            await _save.Handle(NewArticle("s", type: "SECTOR_UPDATE"), CancellationToken.None);
            Assert.That((await _decide.Handle(new DecideHealthArticleRequestModel { Slug = "s", Action = "DELETE" }, CancellationToken.None)).StatusCode, Is.EqualTo(400));
        }

        // ------------------------------------------------------------------------------------------ CMS reads

        [Test]
        public async Task AdminList_ReturnsEveryStatus_AndFilters()
        {
            await _save.Handle(NewArticle("a"), CancellationToken.None);
            await PublishedSector("b");

            var all = await _admin.Handle(new GetHealthArticlesAdminRequestModel(), CancellationToken.None);
            var published = await _admin.Handle(new GetHealthArticlesAdminRequestModel { Status = "PUBLISHED" }, CancellationToken.None);
            var medical = await _admin.Handle(new GetHealthArticlesAdminRequestModel { Type = "MEDICAL" }, CancellationToken.None);

            Assert.That(all.Articles, Has.Count.EqualTo(2));
            Assert.That(published.Articles.Select(x => x.Slug), Is.EqualTo(new[] { "b" }));
            Assert.That(medical.Articles.Select(x => x.Slug), Is.EqualTo(new[] { "a" }));
        }

        [Test]
        public async Task History_RecordsWhoDidWhat_NewestFirst_WithReadableLabels()
        {
            await _save.Handle(new SaveHealthArticleRequestModel { IsCreate = true, Slug = "s", Title = "T", Content = "C", Type = "SECTOR_UPDATE", ActorName = "Priya" }, CancellationToken.None);
            await _decide.Handle(new DecideHealthArticleRequestModel { Slug = "s", Action = "APPROVE", ActorName = "Ravi" }, CancellationToken.None);

            var h = await _history.Handle(new GetHealthArticleHistoryRequestModel { Slug = "s" }, CancellationToken.None);
            var missing = await _history.Handle(new GetHealthArticleHistoryRequestModel { Slug = "nope" }, CancellationToken.None);

            Assert.That(h.Found, Is.True);
            Assert.That(h.Entries.Select(e => e.Action), Is.EqualTo(new[] { "Approved and published", "Created the article" }));
            Assert.That(h.Entries[0].Actor, Is.EqualTo("Ravi"));
            Assert.That(h.Entries[1].Actor, Is.EqualTo("Priya"));
            Assert.That(missing.Found, Is.False);
        }

        [Test]
        public async Task Summary_CountsPendingContributorsAndOpenTopics()
        {
            AddContributor("INDEPENDENT_DOCTOR", status: "PENDING");
            AddContributor("WRITER", status: "VERIFIED", name: "W");
            var c = AddContributor("HEALTH_WORKER", status: "PENDING", name: "H");
            _context.HealthWikiTopicRequests.Add(new HealthWikiTopicRequest { TopicId = Guid.NewGuid(), ContributorId = c.ContributorId, Title = "t", Outline = "o", Status = "SUBMITTED", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            _context.HealthWikiTopicRequests.Add(new HealthWikiTopicRequest { TopicId = Guid.NewGuid(), ContributorId = c.ContributorId, Title = "t2", Outline = "o", Status = "DECLINED", DecisionNote = "no", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            await _context.SaveChangesAsync();

            var s = await new GetHealthWikiSummaryHandler(_context).Handle(new GetHealthWikiSummaryRequestModel(), CancellationToken.None);

            Assert.That(s.PendingContributors, Is.EqualTo(2));
            Assert.That(s.OpenTopics, Is.EqualTo(1));
        }

        [Test]
        public async Task Conditions_ListDefaultsPlusSlugsAlreadyUsed()
        {
            var a = NewArticle("a"); a.RelatedConditionSlug = "kidney-disease";
            await _save.Handle(a, CancellationToken.None);
            var config = new ConfigurationBuilder().Build();

            var res = await new GetHealthWikiConditionsHandler(_context, config).Handle(new GetHealthWikiConditionsRequestModel(), CancellationToken.None);

            Assert.That(res.Conditions, Does.Contain("diabetes"));
            Assert.That(res.Conditions, Does.Contain("kidney-disease"));
            Assert.That(res.Conditions, Is.Ordered);
        }
    }
}
