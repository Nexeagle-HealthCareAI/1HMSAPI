using System;
using System.Collections.Generic;
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
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class ContributorPortalTests
    {
        private AppDbContext _context = null!;
        private CancellationToken Ct => CancellationToken.None;

        [SetUp]
        public void SetUp() => _context = InMemoryDbContextFactory.CreateContext();

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private long _mobileSeed = 9000000100;

        private HealthWikiContributor Person(string type = "INDEPENDENT_DOCTOR", string status = "VERIFIED", string name = "Dr. Meera Nair")
        {
            var c = new HealthWikiContributor
            {
                ContributorId = Guid.NewGuid(), Type = type, FullName = name, Mobile = (_mobileSeed++).ToString(), Status = status,
                Qualification = "MBBS, MD", Speciality = "Endocrinologist", RegistrationNumber = "48213", RegistrationCouncil = "Delhi Medical Council",
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            _context.HealthWikiContributors.Add(c);
            _context.SaveChanges();
            return c;
        }

        private static SaveContributorArticleRequestModel Draft(Guid me, string? slug = null, string type = "MEDICAL", string title = "Living with High Blood Pressure", string content = "## Know your numbers") => new()
        {
            ContributorId = me, Slug = slug, Type = type, Title = title, Description = "Habits that help.", Content = content,
        };

        private async Task<string> NewDraft(HealthWikiContributor author, string type = "MEDICAL", string title = "Living with High Blood Pressure")
        {
            var res = await new SaveContributorArticleHandler(_context).Handle(Draft(author.ContributorId, null, type, title), Ct);
            Assert.That(res.Success, Is.True, res.Message);
            return res.Data!.Slug;
        }

        /// <summary>A MEDICAL article by <paramref name="author"/>, assigned to <paramref name="reviewer"/> and sent for review.</summary>
        private async Task<string> InReview(HealthWikiContributor author, HealthWikiContributor reviewer)
        {
            var slug = await NewDraft(author);
            var a = _context.HealthArticles.Single(x => x.Slug == slug);
            a.ReviewerContributorId = reviewer.ContributorId;
            await _context.SaveChangesAsync();
            var sub = await new SubmitContributorArticleHandler(_context).Handle(new SubmitContributorArticleRequestModel { ContributorId = author.ContributorId, Slug = slug }, Ct);
            Assert.That(sub.Success, Is.True, sub.Message);
            return slug;
        }

        private Task<EasyHMSAPI.Application.ResponseModels.ApiResult<EasyHMSAPI.Application.ResponseModels.QueryResponseModels.MyArticleInfo>> Review(
            HealthWikiContributor who, string slug, string decision, string? comment = null, bool accuracy = true) =>
            new ReviewContributorArticleHandler(_context).Handle(new ReviewContributorArticleRequestModel
            { ContributorId = who.ContributorId, Slug = slug, Decision = decision, Comment = comment, AccuracyConfirmed = accuracy }, Ct);

        private async Task<List<EasyHMSAPI.Application.ResponseModels.QueryResponseModels.PublicHealthArticleInfo>> PublicList() =>
            (await new GetPublicHealthArticlesHandler(_context).Handle(new GetPublicHealthArticlesRequestModel(), Ct)).Articles;

        // ------------------------------------------------------------------------------------------ rules

        [TestCase("Living with High BP!", "living-with-high-bp")]
        [TestCase("  --Diabetes: Type 2--  ", "diabetes-type-2")]
        [TestCase("मधुमेह", "article")]
        [TestCase("", "article")]
        public void Slugify_ProducesValidSlugs(string title, string expected)
        {
            var slug = ContributorPortalRules.Slugify(title);
            Assert.That(slug, Is.EqualTo(expected));
            Assert.That(HealthArticleRules.IsValidSlug(slug), Is.True);
        }

        [Test]
        public void Slugify_CapsLength() =>
            Assert.That(ContributorPortalRules.Slugify(new string('a', 300)).Length, Is.LessThanOrEqualTo(80));

        [TestCase("2010", true)]
        [TestCase("1949", false)]
        [TestCase("2999", false)]
        [TestCase("abcd", false)]
        [TestCase("", false)]
        public void RegistrationYear_MustBeARealYear(string input, bool ok) =>
            Assert.That(ContributorPortalRules.TryParseRegistrationYear(input, out _), Is.EqualTo(ok));

        // ------------------------------------------------------------------------------------------ register / me

        private RegisterContributorRequestModel Reg(Guid id, Action<RegisterContributorRequestModel>? tweak = null)
        {
            var r = new RegisterContributorRequestModel
            {
                ContributorId = id, FullName = " Dr. Meera Nair ", Speciality = "Endocrinologist", Qualification = "MBBS, MD",
                RegistrationNumber = "48213", RegistrationCouncil = "Delhi Medical Council", RegistrationYear = "2010", Consent = true,
            };
            tweak?.Invoke(r);
            return r;
        }

        [Test]
        public async Task Register_Doctor_MovesFromInvitedToPending_AndKeepsConsent()
        {
            var c = Person(status: "INVITED", name: "");
            var res = await new RegisterContributorHandler(_context).Handle(Reg(c.ContributorId), Ct);

            Assert.That(res.Success, Is.True, res.Message);
            Assert.That(res.Data!.Status, Is.EqualTo("PENDING"));
            Assert.That(res.Data.FullName, Is.EqualTo("Dr. Meera Nair"));
            Assert.That(res.Data.MobileMasked, Does.Contain("•"));
            var row = _context.HealthWikiContributors.Single();
            Assert.That(row.ConsentAt, Is.Not.Null);
            Assert.That(row.ConsentVersion, Is.EqualTo(ContributorPortalRules.ConsentVersion));
            Assert.That(row.RegistrationYear, Is.EqualTo(2010));
        }

        [Test]
        public async Task Register_Validation()
        {
            var c = Person(status: "INVITED");
            var h = new RegisterContributorHandler(_context);

            Assert.That((await h.Handle(Reg(c.ContributorId, r => r.FullName = " "), Ct)).Message, Does.Contain("full name"));
            Assert.That((await h.Handle(Reg(c.ContributorId, r => r.Consent = false), Ct)).Message, Does.Contain("consent"));
            Assert.That((await h.Handle(Reg(c.ContributorId, r => r.RegistrationNumber = ""), Ct)).Message, Does.Contain("Registration number"));
            Assert.That((await h.Handle(Reg(c.ContributorId, r => r.RegistrationYear = "1800"), Ct)).StatusCode, Is.EqualTo(400));
            Assert.That((await h.Handle(Reg(c.ContributorId, r => r.Speciality = ""), Ct)).StatusCode, Is.EqualTo(400));
            Assert.That((await h.Handle(Reg(c.ContributorId, r => r.PhotoUrl = "http://x/a.jpg"), Ct)).StatusCode, Is.EqualTo(400));
            Assert.That(_context.HealthWikiContributors.Single().Status, Is.EqualTo("INVITED"), "nothing saved while invalid");
        }

        [Test]
        public async Task Register_HealthWorker_NeedsAJobTitle_AndStoresNoRegistration()
        {
            var w = Person("HEALTH_WORKER", "INVITED", "Asha");
            var h = new RegisterContributorHandler(_context);

            var none = await h.Handle(new RegisterContributorRequestModel { ContributorId = w.ContributorId, FullName = "Asha", Consent = true }, Ct);
            var ok = await h.Handle(new RegisterContributorRequestModel { ContributorId = w.ContributorId, FullName = "Asha", RoleTitle = "ANM", Organisation = "PHC Kota", RegistrationNumber = "ignored", Consent = true }, Ct);

            Assert.That(none.Message, Does.Contain("job title"));
            Assert.That(ok.Success, Is.True, ok.Message);
            Assert.That(ok.Data!.RegistrationNumber, Is.Null);
            Assert.That(ok.Data.RoleTitle, Is.EqualTo("ANM"));
        }

        [Test]
        public async Task Register_ChangingTheRegistrationOfAVerifiedDoctor_SendsThemBackToPending()
        {
            var c = Person(status: "VERIFIED");
            c.VerifiedAt = DateTime.UtcNow; c.RegistrationYear = 2010;
            await _context.SaveChangesAsync();
            var h = new RegisterContributorHandler(_context);

            var same = await h.Handle(Reg(c.ContributorId), Ct);
            Assert.That(same.Data!.Status, Is.EqualTo("VERIFIED"), "editing the name or bio keeps the verification");

            var changed = await h.Handle(Reg(c.ContributorId, r => r.RegistrationNumber = "99999"), Ct);
            Assert.That(changed.Data!.Status, Is.EqualTo("PENDING"));
            Assert.That(_context.HealthWikiContributors.Single().VerifiedAt, Is.Null);
        }

        [Test]
        public async Task Register_NotForHospitalDoctorsStaffOrRejected()
        {
            var hospital = Person("HOSPITAL_DOCTOR");
            var rejected = Person("WRITER", "REJECTED", "R");
            var h = new RegisterContributorHandler(_context);

            Assert.That((await h.Handle(Reg(hospital.ContributorId), Ct)).StatusCode, Is.EqualTo(403));
            Assert.That((await h.Handle(Reg(rejected.ContributorId), Ct)).StatusCode, Is.EqualTo(403));
        }

        [Test]
        public async Task Me_ReturnsTheProfile_WithAMaskedNumber()
        {
            var c = Person();
            var res = await new GetContributorMeHandler(_context).Handle(new GetContributorMeRequestModel { ContributorId = c.ContributorId }, Ct);

            Assert.That(res.Data!.Role, Is.EqualTo("INDEPENDENT_DOCTOR"));
            Assert.That(System.Text.Json.JsonSerializer.Serialize(res.Data), Does.Not.Contain(c.Mobile!));
            Assert.That((await new GetContributorMeHandler(_context).Handle(new GetContributorMeRequestModel { ContributorId = Guid.NewGuid() }, Ct)).StatusCode, Is.EqualTo(404));
        }

        // ------------------------------------------------------------------------------------------ writing

        [Test]
        public async Task Create_APendingProfileCanDraft_AnInvitedOneCannot()
        {
            var pending = Person(status: "PENDING");
            var invited = Person(status: "INVITED", name: "Not yet");

            var ok = await new SaveContributorArticleHandler(_context).Handle(Draft(pending.ContributorId), Ct);
            var no = await new SaveContributorArticleHandler(_context).Handle(Draft(invited.ContributorId), Ct);

            Assert.That(ok.StatusCode, Is.EqualTo(201));
            Assert.That(ok.Data!.Status, Is.EqualTo("DRAFT"));
            Assert.That(ok.Data.MyRole, Is.EqualTo("AUTHOR"));
            Assert.That(ok.Data.Slug, Is.EqualTo("living-with-high-blood-pressure"));
            Assert.That(no.StatusCode, Is.EqualTo(403));
        }

        [Test]
        public async Task Create_SameTitleTwice_GetsAUniqueSlug()
        {
            var c = Person();
            var a = await NewDraft(c);
            var b = await NewDraft(c);
            Assert.That(b, Is.EqualTo(a + "-2"));
        }

        [Test]
        public async Task Create_AWriterOrHealthWorkerCannotWriteMedical_ButCanWriteSectorUpdates()
        {
            var writer = Person("WRITER", "VERIFIED", "W");
            var worker = Person("HEALTH_WORKER", "VERIFIED", "H");
            var h = new SaveContributorArticleHandler(_context);

            Assert.That((await h.Handle(Draft(writer.ContributorId, type: "MEDICAL"), Ct)).StatusCode, Is.EqualTo(403));
            Assert.That((await h.Handle(Draft(worker.ContributorId, type: "MEDICAL"), Ct)).StatusCode, Is.EqualTo(403));
            Assert.That((await h.Handle(Draft(writer.ContributorId, type: "SECTOR_UPDATE", title: "Telemedicine rules"), Ct)).StatusCode, Is.EqualTo(201));
        }

        [Test]
        public async Task Create_ADraftMayHaveNoTextYet_ButNeedsATitle()
        {
            var c = Person();
            var h = new SaveContributorArticleHandler(_context);

            Assert.That((await h.Handle(Draft(c.ContributorId, content: ""), Ct)).StatusCode, Is.EqualTo(201));
            Assert.That((await h.Handle(Draft(c.ContributorId, title: " "), Ct)).StatusCode, Is.EqualTo(400));
        }

        [Test]
        public async Task Create_CoverNeedsAltText_AndOnlyHttps()
        {
            var c = Person();
            var h = new SaveContributorArticleHandler(_context);
            var noAlt = Draft(c.ContributorId); noAlt.CoverImageUrl = "https://cdn.example.com/a.jpg";
            var http = Draft(c.ContributorId); http.CoverImageUrl = "http://cdn.example.com/a.jpg"; http.CoverImageAlt = "x";

            Assert.That((await h.Handle(noAlt, Ct)).StatusCode, Is.EqualTo(400));
            Assert.That((await h.Handle(http, Ct)).StatusCode, Is.EqualTo(400));
        }

        [Test]
        public async Task Edit_OnlyTheAuthor_AndNotWhileItIsWithTheReviewer()
        {
            var author = Person(name: "Author");
            var other = Person(name: "Other");
            var reviewer = Person(name: "Reviewer");
            var slug = await NewDraft(author);
            var h = new SaveContributorArticleHandler(_context);

            var edited = await h.Handle(Draft(author.ContributorId, slug, title: "New title"), Ct);
            var stranger = await h.Handle(Draft(other.ContributorId, slug, title: "Hijack"), Ct);
            Assert.That(edited.Data!.Title, Is.EqualTo("New title"));
            Assert.That(stranger.StatusCode, Is.EqualTo(404), "someone else's article reads as missing");

            var a = _context.HealthArticles.Single(); a.ReviewerContributorId = reviewer.ContributorId; await _context.SaveChangesAsync();
            await new SubmitContributorArticleHandler(_context).Handle(new SubmitContributorArticleRequestModel { ContributorId = author.ContributorId, Slug = slug }, Ct);
            Assert.That((await h.Handle(Draft(author.ContributorId, slug, title: "Late edit"), Ct)).StatusCode, Is.EqualTo(409));
        }

        [Test]
        public async Task Edit_OfALiveArticle_KeepsTheLiveVersionOnline_UntilTheEditIsApproved()
        {
            var author = Person(name: "Author"); var reviewer = Person(name: "Reviewer");
            var slug = await InReview(author, reviewer);
            await Review(reviewer, slug, "APPROVE");
            Assert.That((await PublicList()).Single().Title, Is.EqualTo("Living with High Blood Pressure"));

            var edit = await new SaveContributorArticleHandler(_context).Handle(Draft(author.ContributorId, slug, title: "High Blood Pressure, updated"), Ct);
            Assert.That(edit.Data!.Status, Is.EqualTo("PUBLISHED"));
            Assert.That(edit.Data.HasPendingRevision, Is.True);
            Assert.That(edit.Data.Title, Is.EqualTo("High Blood Pressure, updated"), "the author sees their working copy");
            Assert.That((await PublicList()).Single().Title, Is.EqualTo("Living with High Blood Pressure"), "readers still see the live text");

            await new SubmitContributorArticleHandler(_context).Handle(new SubmitContributorArticleRequestModel { ContributorId = author.ContributorId, Slug = slug }, Ct);
            var pending = await new GetContributorArticlesHandler(_context).Handle(new GetContributorArticlesRequestModel { ContributorId = reviewer.ContributorId }, Ct);
            Assert.That(pending.Data!.Pending.Select(p => p.Slug), Is.EqualTo(new[] { slug }), "the edit is waiting for the reviewer");
            Assert.That((await PublicList()).Single().Title, Is.EqualTo("Living with High Blood Pressure"), "still the old text while in review");

            var done = await Review(reviewer, slug, "APPROVE");
            Assert.That(done.Success, Is.True, done.Message);
            Assert.That((await PublicList()).Single().Title, Is.EqualTo("High Blood Pressure, updated"));
        }

        // ------------------------------------------------------------------------------------------ submit

        [Test]
        public async Task Submit_NeedsAVerifiedProfile_AndRealContent()
        {
            var pending = Person(status: "PENDING", name: "Pending");
            var verified = Person(name: "Verified");
            var slugP = await NewDraft(pending);
            var slugV = await new SaveContributorArticleHandler(_context).Handle(Draft(verified.ContributorId, content: ""), Ct);
            var h = new SubmitContributorArticleHandler(_context);

            Assert.That((await h.Handle(new SubmitContributorArticleRequestModel { ContributorId = pending.ContributorId, Slug = slugP }, Ct)).StatusCode, Is.EqualTo(403));
            var empty = await h.Handle(new SubmitContributorArticleRequestModel { ContributorId = verified.ContributorId, Slug = slugV.Data!.Slug }, Ct);
            Assert.That(empty.StatusCode, Is.EqualTo(400));
            Assert.That(empty.Message, Is.EqualTo("Add a title and some content first."));
        }

        [Test]
        public async Task Submit_AMedicalArticleCanWaitForTheTeamToAssignADoctor()
        {
            var author = Person(name: "Author");
            var slug = await NewDraft(author);

            var res = await new SubmitContributorArticleHandler(_context).Handle(new SubmitContributorArticleRequestModel { ContributorId = author.ContributorId, Slug = slug }, Ct);

            Assert.That(res.Success, Is.True, res.Message);
            Assert.That(res.Data!.Status, Is.EqualTo("IN_REVIEW"));
            Assert.That(res.Data.ReviewerName, Is.Null);
            Assert.That(_context.HealthArticles.Single().SubmittedAt, Is.Not.Null);
        }

        [Test]
        public async Task Submit_OnlyADraft_AndOnlyByItsAuthor()
        {
            var author = Person(name: "Author"); var other = Person(name: "Other");
            var slug = await NewDraft(author);
            var h = new SubmitContributorArticleHandler(_context);

            Assert.That((await h.Handle(new SubmitContributorArticleRequestModel { ContributorId = other.ContributorId, Slug = slug }, Ct)).StatusCode, Is.EqualTo(404));
            await h.Handle(new SubmitContributorArticleRequestModel { ContributorId = author.ContributorId, Slug = slug }, Ct);
            Assert.That((await h.Handle(new SubmitContributorArticleRequestModel { ContributorId = author.ContributorId, Slug = slug }, Ct)).StatusCode, Is.EqualTo(409));
        }

        // ------------------------------------------------------------------------------------------ lists and visibility

        [Test]
        public async Task Lists_GroupByMyRole_AndHideOthersDrafts()
        {
            var author = Person(name: "Author"); var reviewer = Person(name: "Reviewer");
            var draft = await NewDraft(author, title: "Still writing");
            var review = await InReview(author, reviewer);
            _context.HealthArticles.Single(a => a.Slug == draft).ReviewerContributorId = reviewer.ContributorId; // reviewer assigned, but still a draft
            await _context.SaveChangesAsync();

            var mineAuthor = (await new GetContributorArticlesHandler(_context).Handle(new GetContributorArticlesRequestModel { ContributorId = author.ContributorId }, Ct)).Data!;
            var mineReviewer = (await new GetContributorArticlesHandler(_context).Handle(new GetContributorArticlesRequestModel { ContributorId = reviewer.ContributorId }, Ct)).Data!;

            Assert.That(mineAuthor.Drafts.Select(d => d.Slug), Is.EquivalentTo(new[] { draft, review }));
            Assert.That(mineAuthor.Pending, Is.Empty, "the author does not review their own work");
            Assert.That(mineReviewer.Pending.Select(d => d.Slug), Is.EqualTo(new[] { review }));
            Assert.That(mineReviewer.Pending[0].ReviewerName, Is.EqualTo("Reviewer"));
            Assert.That(mineReviewer.Pending[0].AuthorName, Is.EqualTo("Author"));
            Assert.That(mineReviewer.Pending[0].WaitingDays, Is.EqualTo(0));
            Assert.That(mineReviewer.Drafts, Is.Empty);

            var hidden = await new GetContributorArticleHandler(_context).Handle(new GetContributorArticleRequestModel { ContributorId = reviewer.ContributorId, Slug = draft }, Ct);
            Assert.That(hidden.StatusCode, Is.EqualTo(404), "a reviewer cannot read a draft that is not with them yet");
        }

        [Test]
        public async Task Get_AStrangerCannotReadAnArticle()
        {
            var author = Person(name: "Author"); var stranger = Person(name: "Stranger");
            var slug = await NewDraft(author);
            var res = await new GetContributorArticleHandler(_context).Handle(new GetContributorArticleRequestModel { ContributorId = stranger.ContributorId, Slug = slug }, Ct);
            Assert.That(res.StatusCode, Is.EqualTo(404));
        }

        // ------------------------------------------------------------------------------------------ review

        [Test]
        public async Task Review_Approve_ByAVerifiedDoctor_PublishesAtOnce_AndKeepsTheDecision()
        {
            var author = Person(name: "Author"); var reviewer = Person(name: "Dr. Imran Qureshi");
            var slug = await InReview(author, reviewer);

            var res = await Review(reviewer, slug, "approve");

            Assert.That(res.Success, Is.True, res.Message);
            Assert.That(res.Data!.Status, Is.EqualTo("PUBLISHED"));
            Assert.That(res.Data.AwaitingVerification, Is.False);
            var live = (await PublicList()).Single();
            Assert.That(live.Reviewer!.FullName, Is.EqualTo("Dr. Imran Qureshi"));
            Assert.That(live.Reviewer.IsRegistrationVerified, Is.True);
            var review = _context.HealthArticleReviews.Single();
            Assert.That(review.Decision, Is.EqualTo("APPROVE"));
            Assert.That(review.AccuracyConfirmed, Is.True);
            Assert.That(review.RegistrationSnapshot, Does.Contain("Reg. 48213"));
        }

        [Test]
        public async Task Review_Approve_NeedsTheAccuracyConfirmation()
        {
            var author = Person(name: "Author"); var reviewer = Person(name: "Reviewer");
            var slug = await InReview(author, reviewer);

            var res = await Review(reviewer, slug, "APPROVE", accuracy: false);

            Assert.That(res.StatusCode, Is.EqualTo(400));
            Assert.That(_context.HealthArticles.Single().Status, Is.EqualTo("IN_REVIEW"));
            Assert.That(_context.HealthArticleReviews.Count(), Is.EqualTo(0));
        }

        [Test]
        public async Task Review_ByAnUnverifiedDoctor_WaitsAndGoesLiveWhenTheTeamVerifiesThem()
        {
            var author = Person(name: "Author"); var reviewer = Person(status: "PENDING", name: "Dr. New");
            var slug = await InReview(author, reviewer);

            var res = await Review(reviewer, slug, "APPROVE");

            Assert.That(res.Success, Is.True, res.Message);
            Assert.That(res.Data!.AwaitingVerification, Is.True);
            Assert.That(res.Data.Status, Is.EqualTo("PUBLISHED"), "the reviewer's view, as the pages expect");
            Assert.That(_context.HealthArticles.Single().Status, Is.EqualTo("IN_REVIEW"), "but it is not live");
            Assert.That(await PublicList(), Is.Empty);
            var mine = (await new GetContributorArticlesHandler(_context).Handle(new GetContributorArticlesRequestModel { ContributorId = reviewer.ContributorId }, Ct)).Data!;
            Assert.That(mine.Pending, Is.Empty, "nothing left to decide");

            var verified = await new VerifyContributorHandler(_context).Handle(new VerifyContributorRequestModel { ContributorId = reviewer.ContributorId, ActorName = "Priya" }, Ct);

            Assert.That(verified.Success, Is.True, verified.Message);
            var live = (await PublicList()).Single();
            Assert.That(live.Reviewer!.IsRegistrationVerified, Is.True);
            Assert.That(_context.HealthArticles.Single().Status, Is.EqualTo("PUBLISHED"));
        }

        [Test]
        public async Task Review_RequestChanges_NeedsARealComment_AndSendsItBackToTheAuthor()
        {
            var author = Person(name: "Author"); var reviewer = Person(name: "Reviewer");
            var slug = await InReview(author, reviewer);

            var tooShort = await Review(reviewer, slug, "REQUEST_CHANGES", "fix it");
            var ok = await Review(reviewer, slug, "REQUEST_CHANGES", "Please add when to see a doctor.");

            Assert.That(tooShort.StatusCode, Is.EqualTo(400));
            Assert.That(ok.Success, Is.True, ok.Message);
            Assert.That(_context.HealthArticles.Single().Status, Is.EqualTo("DRAFT"));
            var back = await new GetContributorArticleHandler(_context).Handle(new GetContributorArticleRequestModel { ContributorId = author.ContributorId, Slug = slug }, Ct);
            Assert.That(back.Data!.Status, Is.EqualTo("DRAFT"));
            Assert.That(back.Data.ReturnedComment, Is.EqualTo("Please add when to see a doctor."));
            Assert.That(await PublicList(), Is.Empty);
        }

        [Test]
        public async Task Review_OnlyTheAssignedDoctor_OnlyWhileItIsWithThem()
        {
            var author = Person(name: "Author"); var reviewer = Person(name: "Reviewer"); var other = Person(name: "Other Dr");
            var slug = await InReview(author, reviewer);

            Assert.That((await Review(other, slug, "APPROVE")).StatusCode, Is.EqualTo(404), "not their article");
            Assert.That((await Review(author, slug, "APPROVE")).StatusCode, Is.EqualTo(404), "an author cannot review");
            Assert.That((await Review(reviewer, slug, "MAYBE")).StatusCode, Is.EqualTo(400));
            await Review(reviewer, slug, "APPROVE");
            Assert.That((await Review(reviewer, slug, "APPROVE")).StatusCode, Is.EqualTo(409), "already decided");
        }

        [Test]
        public async Task Review_AnEditToALiveArticle_NeedsAVerifiedDoctor()
        {
            var author = Person(name: "Author"); var reviewer = Person(name: "Reviewer");
            var slug = await InReview(author, reviewer);
            await Review(reviewer, slug, "APPROVE");
            await new SaveContributorArticleHandler(_context).Handle(Draft(author.ContributorId, slug, title: "Edited"), Ct);
            await new SubmitContributorArticleHandler(_context).Handle(new SubmitContributorArticleRequestModel { ContributorId = author.ContributorId, Slug = slug }, Ct);
            var row = _context.HealthWikiContributors.Single(c => c.ContributorId == reviewer.ContributorId);
            row.Status = "PENDING"; await _context.SaveChangesAsync(); // their registration is being re-checked

            var res = await Review(row, slug, "APPROVE");

            Assert.That(res.StatusCode, Is.EqualTo(403));
            Assert.That((await PublicList()).Single().Title, Is.EqualTo("Living with High Blood Pressure"));
        }

        [Test]
        public async Task Review_RequestChangesOnAnEdit_LeavesTheLiveVersionOnline()
        {
            var author = Person(name: "Author"); var reviewer = Person(name: "Reviewer");
            var slug = await InReview(author, reviewer);
            await Review(reviewer, slug, "APPROVE");
            await new SaveContributorArticleHandler(_context).Handle(Draft(author.ContributorId, slug, title: "Edited"), Ct);
            await new SubmitContributorArticleHandler(_context).Handle(new SubmitContributorArticleRequestModel { ContributorId = author.ContributorId, Slug = slug }, Ct);

            var res = await Review(reviewer, slug, "REQUEST_CHANGES", "The new dose is wrong, please check.");

            Assert.That(res.Success, Is.True, res.Message);
            Assert.That((await PublicList()).Single().Title, Is.EqualTo("Living with High Blood Pressure"));
            var mine = await new GetContributorArticleHandler(_context).Handle(new GetContributorArticleRequestModel { ContributorId = author.ContributorId, Slug = slug }, Ct);
            Assert.That(mine.Data!.RevisionStatus, Is.EqualTo("DRAFT"));
            Assert.That(mine.Data.ReturnedComment, Is.EqualTo("The new dose is wrong, please check."));
        }

        [Test]
        public async Task TheAuthorCannotBeTheirOwnReviewer()
        {
            var author = Person(name: "Author");
            var slug = await NewDraft(author);

            var res = await new SaveHealthArticleHandler(_context).Handle(new SaveHealthArticleRequestModel
            { Slug = slug, ReviewerContributorId = author.ContributorId }, Ct);

            Assert.That(res.StatusCode, Is.EqualTo(400));
            Assert.That(res.Message, Does.Contain("own article"));
        }

        [Test]
        public async Task EditingTheTextAfterApproval_VoidsTheApproval_SoItCannotGoLiveOnIt()
        {
            var author = Person(name: "Author"); var reviewer = Person(status: "PENDING", name: "Dr. New");
            var slug = await InReview(author, reviewer);
            await Review(reviewer, slug, "APPROVE"); // approved, waiting for verification
            Assert.That(_context.HealthArticles.Single().ApprovedAt, Is.Not.Null);

            var edit = await new SaveHealthArticleHandler(_context).Handle(new SaveHealthArticleRequestModel { Slug = slug, Content = "Quietly changed text" }, Ct);
            Assert.That(edit.Success, Is.True, edit.Message);
            Assert.That(_context.HealthArticles.Single().ApprovedAt, Is.Null);

            await new VerifyContributorHandler(_context).Handle(new VerifyContributorRequestModel { ContributorId = reviewer.ContributorId }, Ct);
            Assert.That(await PublicList(), Is.Empty, "verification does not publish text the doctor never approved");
        }

        // ------------------------------------------------------------------------------------------ topics

        private static CreateContributorTopicRequestModel Topic(Guid me, string title = "Recognising a silent heart attack", string type = "MEDICAL") => new()
        {
            ContributorId = me, Title = title, Type = type, Outline = "Symptoms that are easy to miss.", WhyItMatters = "People wait too long.",
        };

        [Test]
        public async Task Topic_Create_NeedsAcceptedProfile_AndAllThreeFields()
        {
            var invited = Person(status: "INVITED", name: "Not yet");
            var doctor = Person();
            var h = new CreateContributorTopicHandler(_context);

            Assert.That((await h.Handle(Topic(invited.ContributorId), Ct)).StatusCode, Is.EqualTo(403));
            var noWhy = Topic(doctor.ContributorId); noWhy.WhyItMatters = " ";
            Assert.That((await h.Handle(noWhy, Ct)).StatusCode, Is.EqualTo(400));
            var ok = await h.Handle(Topic(doctor.ContributorId), Ct);
            Assert.That(ok.StatusCode, Is.EqualTo(201));
            Assert.That(ok.Data!.Status, Is.EqualTo("SUBMITTED"));
        }

        [Test]
        public async Task Topic_AWriterSuggestsOnlySectorUpdates()
        {
            var writer = Person("WRITER", "VERIFIED", "W");
            var h = new CreateContributorTopicHandler(_context);

            Assert.That((await h.Handle(Topic(writer.ContributorId, type: "MEDICAL"), Ct)).StatusCode, Is.EqualTo(403));
            Assert.That((await h.Handle(Topic(writer.ContributorId, "Telemedicine rules", "SECTOR_UPDATE"), Ct)).StatusCode, Is.EqualTo(201));
        }

        [Test]
        public async Task Topic_AtMostFiveOpenAtATime()
        {
            var doctor = Person();
            var h = new CreateContributorTopicHandler(_context);
            for (var i = 0; i < 5; i++) Assert.That((await h.Handle(Topic(doctor.ContributorId, $"Topic {i}"), Ct)).Success, Is.True);

            var sixth = await h.Handle(Topic(doctor.ContributorId, "One more"), Ct);
            Assert.That(sixth.StatusCode, Is.EqualTo(409));

            _context.HealthWikiTopicRequests.First().Status = "DECLINED"; _context.HealthWikiTopicRequests.First().DecisionNote = "no";
            await _context.SaveChangesAsync();
            Assert.That((await h.Handle(Topic(doctor.ContributorId, "Now it fits"), Ct)).Success, Is.True, "a decided request frees a place");
        }

        [Test]
        public async Task Topic_CmsDecisions_Accept_Decline_AskDetail()
        {
            var doctor = Person();
            var create = new CreateContributorTopicHandler(_context);
            var decide = new DecideTopicHandler(_context);
            var accept = (await create.Handle(Topic(doctor.ContributorId, "Silent heart attack"), Ct)).Data!;
            var decline = (await create.Handle(Topic(doctor.ContributorId, "Off topic"), Ct)).Data!;
            var ask = (await create.Handle(Topic(doctor.ContributorId, "Vague one"), Ct)).Data!;

            Assert.That((await decide.Handle(new DecideTopicRequestModel { TopicId = decline.Id, Action = "DECLINE" }, Ct)).StatusCode, Is.EqualTo(400), "a reason is required");
            Assert.That((await decide.Handle(new DecideTopicRequestModel { TopicId = ask.Id, Action = "ASK_DETAIL", Note = " " }, Ct)).StatusCode, Is.EqualTo(400));
            Assert.That((await decide.Handle(new DecideTopicRequestModel { TopicId = accept.Id, Action = "DELETE" }, Ct)).StatusCode, Is.EqualTo(400));

            var accepted = await decide.Handle(new DecideTopicRequestModel { TopicId = accept.Id, Action = "ACCEPT", ActorName = "Priya" }, Ct);
            await decide.Handle(new DecideTopicRequestModel { TopicId = decline.Id, Action = "DECLINE", Note = "Out of scope." }, Ct);
            await decide.Handle(new DecideTopicRequestModel { TopicId = ask.Id, Action = "ASK_DETAIL", Note = "Which age group?" }, Ct);

            Assert.That(accepted.Data!.Status, Is.EqualTo("ACCEPTED"));
            Assert.That(accepted.Data.ArticleSlug, Is.EqualTo("silent-heart-attack"));
            var draft = _context.HealthArticles.Single();
            Assert.That(draft.Status, Is.EqualTo("DRAFT"));
            Assert.That(draft.AuthorContributorId, Is.EqualTo(doctor.ContributorId));
            Assert.That(draft.Content, Does.Contain("Symptoms that are easy to miss."));
            Assert.That((await decide.Handle(new DecideTopicRequestModel { TopicId = accept.Id, Action = "DECLINE", Note = "again" }, Ct)).StatusCode, Is.EqualTo(409), "decided is final");

            var mine = (await new GetContributorTopicsHandler(_context).Handle(new GetContributorTopicsRequestModel { ContributorId = doctor.ContributorId }, Ct)).Data!;
            Assert.That(mine.Single(t => t.Id == decline.Id).DecisionNote, Is.EqualTo("Out of scope."));
            Assert.That(mine.Single(t => t.Id == ask.Id).Status, Is.EqualTo("NEEDS_DETAIL"));
            Assert.That(mine.Single(t => t.Id == accept.Id).ArticleSlug, Is.EqualTo("silent-heart-attack"));
        }

        [Test]
        public async Task Topic_AddingDetail_OnlyWhenAskedFor_AndItGoesBackToSubmitted()
        {
            var doctor = Person();
            var topic = (await new CreateContributorTopicHandler(_context).Handle(Topic(doctor.ContributorId), Ct)).Data!;
            var add = new AddContributorTopicDetailHandler(_context);
            var detail = new AddContributorTopicDetailRequestModel
            { ContributorId = doctor.ContributorId, TopicId = topic.Id, Outline = "Adults over 50.", Provided = new HashSet<string> { "outline" } };

            Assert.That((await add.Handle(detail, Ct)).StatusCode, Is.EqualTo(409), "the team has not asked yet");
            await new DecideTopicHandler(_context).Handle(new DecideTopicRequestModel { TopicId = topic.Id, Action = "ASK_DETAIL", Note = "Which age group?" }, Ct);
            var other = Person(name: "Other");
            Assert.That((await add.Handle(new AddContributorTopicDetailRequestModel { ContributorId = other.ContributorId, TopicId = topic.Id, Outline = "x", Provided = new HashSet<string> { "outline" } }, Ct)).StatusCode, Is.EqualTo(404));

            var res = await add.Handle(detail, Ct);

            Assert.That(res.Success, Is.True, res.Message);
            Assert.That(res.Data!.Status, Is.EqualTo("SUBMITTED"));
            Assert.That(res.Data.DecisionNote, Is.Null);
            Assert.That(res.Data.Outline, Is.EqualTo("Adults over 50."));
            Assert.That(res.Data.WhyItMatters, Is.EqualTo("People wait too long."), "fields not sent are unchanged");
        }

        [Test]
        public async Task Topic_WhenTheAuthorStartsWritingTheAcceptedDraft_ItBecomesArticleStarted()
        {
            var doctor = Person();
            var topic = (await new CreateContributorTopicHandler(_context).Handle(Topic(doctor.ContributorId, "Silent heart attack"), Ct)).Data!;
            var accepted = (await new DecideTopicHandler(_context).Handle(new DecideTopicRequestModel { TopicId = topic.Id, Action = "ACCEPT" }, Ct)).Data!;

            var edit = await new SaveContributorArticleHandler(_context).Handle(Draft(doctor.ContributorId, accepted.ArticleSlug, title: "Silent heart attack", content: "My first paragraph."), Ct);

            Assert.That(edit.Success, Is.True, edit.Message);
            Assert.That(_context.HealthWikiTopicRequests.Single().Status, Is.EqualTo("ARTICLE_STARTED"));
        }

        [Test]
        public async Task TopicAdmin_ListFilterAndGet()
        {
            var doctor = Person();
            var create = new CreateContributorTopicHandler(_context);
            var a = (await create.Handle(Topic(doctor.ContributorId, "A"), Ct)).Data!;
            await create.Handle(Topic(doctor.ContributorId, "B"), Ct);
            await new DecideTopicHandler(_context).Handle(new DecideTopicRequestModel { TopicId = a.Id, Action = "DECLINE", Note = "no" }, Ct);
            var h = new GetTopicRequestsAdminHandler(_context);

            Assert.That((await h.Handle(new GetTopicRequestsAdminRequestModel(), Ct)).Data!, Has.Count.EqualTo(2));
            Assert.That((await h.Handle(new GetTopicRequestsAdminRequestModel { Status = "submitted" }, Ct)).Data!.Select(t => t.Title), Is.EqualTo(new[] { "B" }));
            Assert.That((await h.Handle(new GetTopicRequestsAdminRequestModel { TopicId = a.Id }, Ct)).Data!.Single().DecisionReason, Is.EqualTo("no"));
            Assert.That((await h.Handle(new GetTopicRequestsAdminRequestModel { TopicId = Guid.NewGuid() }, Ct)).StatusCode, Is.EqualTo(404));
        }
    }
}
