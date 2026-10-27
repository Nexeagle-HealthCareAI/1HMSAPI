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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class HealthWikiStaffTests
    {
        private AppDbContext _context = null!;
        private HealthWikiStaffService _staff = null!;
        private CancellationToken Ct => CancellationToken.None;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _staff = new HealthWikiStaffService(_context, new RecordingNotifier());
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private Doctor AddDoctor(string name = "Dr. Meera Nair", string licence = "48213", string? council = "Delhi Medical Council", int? year = 2010)
        {
            var userId = Guid.NewGuid();
            var d = new Doctor { DoctorID = Guid.NewGuid(), UserID = userId, LicenseNumber = licence, Qualification = "MBBS, MD", MedicalCouncil = council, RegistrationYear = year };
            _context.Doctors.Add(d);
            _context.UserProfiles.Add(new UserProfile { UserProfileID = Guid.NewGuid(), UserID = userId, FullName = name });
            _context.SaveChanges();
            return d;
        }

        // What a CMS admin does after checking the NMC register: marks the doctor's registration as verified (or removes the mark).
        private async Task SetNmcVerified(Doctor d, bool verified = true)
        {
            var row = _context.Doctors.Single(x => x.DoctorID == d.DoctorID);
            row.IsRegistrationVerified = verified;
            row.RegistrationVerifiedAt = verified ? DateTime.UtcNow : null;
            await _context.SaveChangesAsync();
        }

        private HealthWikiContributor AddWriter(string name = "Author")
        {
            var c = new HealthWikiContributor
            {
                ContributorId = Guid.NewGuid(), Type = "INDEPENDENT_DOCTOR", FullName = name, Mobile = "9" + Random.Shared.NextInt64(100000000, 999999999),
                Status = "VERIFIED", RegistrationNumber = "A1", RegistrationCouncil = "MCI", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            _context.HealthWikiContributors.Add(c);
            _context.SaveChanges();
            return c;
        }

        private async Task<string> InReviewFor(HealthWikiContributor reviewer, HealthWikiContributor author)
        {
            var a = new HealthArticle
            {
                ArticleId = Guid.NewGuid(), Slug = "high-blood-pressure", Title = "Living with High Blood Pressure", Description = "d", Content = "## Know your numbers",
                Type = "MEDICAL", Status = "IN_REVIEW", AuthorContributorId = author.ContributorId, ReviewerContributorId = reviewer.ContributorId,
                SubmittedAt = DateTime.UtcNow.AddDays(-4), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            _context.HealthArticles.Add(a);
            await _context.SaveChangesAsync();
            return a.Slug;
        }

        private GetHealthWikiInboxHandler Inbox => new(_context, _staff);
        private GetHealthWikiStaffArticleHandler Detail => new(_context, _staff);
        private ReviewHealthWikiArticleHandler Review => new(_staff, new HealthArticleReviewService(_context, new RecordingNotifier()));

        [Test]
        public async Task TheInbox_ListsMyOwnPublishedArticleUnderPublished_ButNotAsWaitingForMyReview()
        {
            var d = AddDoctor();
            var me = (await _staff.EnsureContributorAsync(d.UserID, Ct))!;
            _context.HealthArticles.Add(new HealthArticle
            {
                ArticleId = Guid.NewGuid(), Slug = "my-own-article", Title = "My own article", Description = "d", Content = "## Hello", Type = "MEDICAL", Status = "PUBLISHED",
                AuthorContributorId = me.ContributorId, ReviewerContributorId = null, PublishedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();

            var inbox = await Inbox.Handle(new GetHealthWikiInboxRequestModel { UserId = d.UserID }, Ct);

            Assert.That(inbox.Published.Select(a => a.Slug), Is.EqualTo(new[] { "my-own-article" }));
            Assert.That(inbox.Pending, Is.Empty);
        }

        // ------------------------------------------------------------------------------------------ hospital doctor as contributor

        [Test]
        public async Task FirstVisit_MakesAPendingContributor_FromTheDoctorsProfile()
        {
            var d = AddDoctor();
            var c = await _staff.EnsureContributorAsync(d.UserID, Ct);

            Assert.That(c, Is.Not.Null);
            Assert.That(c!.Type, Is.EqualTo("HOSPITAL_DOCTOR"));
            Assert.That(c.Status, Is.EqualTo("PENDING"), "not verified until the CMS marks the registration as checked");
            Assert.That(c.DoctorId, Is.EqualTo(d.DoctorID));
            Assert.That(c.FullName, Is.EqualTo("Dr. Meera Nair"));
            Assert.That(c.RegistrationNumber, Is.EqualTo("48213"));
            Assert.That(c.RegistrationCouncil, Is.EqualTo("Delhi Medical Council"));
            Assert.That(c.Mobile, Is.Null, "no mobile is copied");
            Assert.That(_context.HealthWikiAudits.Any(a => a.Action == "JOINED"), Is.True);
        }

        [Test]
        public async Task SecondVisit_ReusesTheRow_AndRefreshesTheProfile()
        {
            var d = AddDoctor();
            var first = await _staff.EnsureContributorAsync(d.UserID, Ct);
            _context.UserProfiles.Single(u => u.UserID == d.UserID).FullName = "Dr. Meera K. Nair";
            await _context.SaveChangesAsync();

            var second = await _staff.EnsureContributorAsync(d.UserID, Ct);

            Assert.That(second!.ContributorId, Is.EqualTo(first!.ContributorId));
            Assert.That(second.FullName, Is.EqualTo("Dr. Meera K. Nair"));
            Assert.That(_context.HealthWikiContributors.Count(), Is.EqualTo(1));
        }

        [Test]
        public async Task APlaceholderLicence_IsNotARegistrationNumber_EvenIfTheCmsMarkedTheDoctor()
        {
            var d = AddDoctor(licence: "PENDING", council: null, year: null);
            await SetNmcVerified(d);

            var c = await _staff.EnsureContributorAsync(d.UserID, Ct);

            Assert.That(c!.RegistrationNumber, Is.Null);
            Assert.That(c.Status, Is.EqualTo("PENDING"), "a flag on a placeholder licence is not a verified registration");
        }

        [Test]
        public async Task TheStatusFollowsTheCmsNmcCheck_InBothDirections()
        {
            var d = AddDoctor();
            var c = await _staff.EnsureContributorAsync(d.UserID, Ct);
            Assert.That(c!.Status, Is.EqualTo("PENDING"));

            await SetNmcVerified(d);
            c = await _staff.EnsureContributorAsync(d.UserID, Ct);
            Assert.That(c!.Status, Is.EqualTo("VERIFIED"));
            Assert.That(c.VerifiedBy, Does.Contain("NMC"));

            await SetNmcVerified(d, verified: false);
            c = await _staff.EnsureContributorAsync(d.UserID, Ct);
            Assert.That(c!.Status, Is.EqualTo("PENDING"), "the badge must not outlive the CMS mark");
            Assert.That(c.VerifiedAt, Is.Null);
            Assert.That(_context.HealthWikiAudits.Any(a => a.Action == "VERIFICATION_REMOVED"), Is.True);
        }

        [Test]
        public async Task ADoctorAlreadyMarkedByTheCms_IsVerifiedOnTheFirstVisit()
        {
            var d = AddDoctor();
            await SetNmcVerified(d);

            var c = await _staff.EnsureContributorAsync(d.UserID, Ct);

            Assert.That(c!.Status, Is.EqualTo("VERIFIED"));
        }

        [Test]
        public async Task TheCmsVerifyButton_ForAHospitalDoctor_PointsToTheDoctorList_UnlessAlreadyMarked()
        {
            var d = AddDoctor();
            var c = (await _staff.EnsureContributorAsync(d.UserID, Ct))!;
            var verify = new VerifyContributorHandler(_context, _staff, new RecordingNotifier());

            var refused = await verify.Handle(new VerifyContributorRequestModel { ContributorId = c.ContributorId }, Ct);
            Assert.That(refused.StatusCode, Is.EqualTo(409));
            Assert.That(refused.Message, Does.Contain("CMS doctor list"));

            await SetNmcVerified(d);
            var ok = await verify.Handle(new VerifyContributorRequestModel { ContributorId = c.ContributorId }, Ct);
            Assert.That(ok.Success, Is.True, ok.Message);
            Assert.That(ok.Contributor!.Status, Is.EqualTo("VERIFIED"));
        }

        [Test]
        public async Task TheCmsContributorList_ShowsHospitalDoctorsAsTheyAreNow()
        {
            var d = AddDoctor();
            await _staff.EnsureContributorAsync(d.UserID, Ct);
            await SetNmcVerified(d);

            var list = await new GetContributorsAdminHandler(_context, _staff).Handle(new GetContributorsAdminRequestModel(), Ct);

            Assert.That(list.Contributors.Single().Status, Is.EqualTo("VERIFIED"));
            Assert.That(list.Contributors.Single().Type, Is.EqualTo("HOSPITAL_DOCTOR"));
        }

        [Test]
        public async Task ThePublicBadge_ForAHospitalDoctor_ReadsTheCmsMarkLive()
        {
            var d = AddDoctor(); var me = (await _staff.EnsureContributorAsync(d.UserID, Ct))!;
            await SetNmcVerified(d);
            var slug = await InReviewFor(me, AddWriter());
            await Review.Handle(new ReviewHealthWikiArticleRequestModel { UserId = d.UserID, Slug = slug, Decision = "APPROVE", AccuracyConfirmed = true }, Ct);
            var get = new GetPublicHealthArticlesHandler(_context);

            Assert.That((await get.Handle(new GetPublicHealthArticlesRequestModel(), Ct)).Articles.Single().Reviewer!.IsRegistrationVerified, Is.True);

            await SetNmcVerified(d, verified: false); // nobody opens the Health Wiki again, yet the badge must go
            Assert.That((await get.Handle(new GetPublicHealthArticlesRequestModel(), Ct)).Articles.Single().Reviewer!.IsRegistrationVerified, Is.False);
        }

        [Test]
        public async Task ANonDoctor_GetsNoContributor_AndARejectedOneIsLeftAlone()
        {
            Assert.That(await _staff.EnsureContributorAsync(Guid.NewGuid(), Ct), Is.Null);
            Assert.That(await _staff.EnsureContributorAsync(Guid.Empty, Ct), Is.Null);

            var d = AddDoctor();
            var c = await _staff.EnsureContributorAsync(d.UserID, Ct);
            _context.HealthWikiContributors.Single().Status = "REJECTED";
            await _context.SaveChangesAsync();
            Assert.That((await _staff.EnsureContributorAsync(d.UserID, Ct))!.Status, Is.EqualTo("REJECTED"));
            Assert.That((await Inbox.Handle(new GetHealthWikiInboxRequestModel { UserId = d.UserID }, Ct)).StatusCode, Is.EqualTo(403));
        }

        // ------------------------------------------------------------------------------------------ inbox and article

        [Test]
        public async Task Inbox_ListsWhatIsWaitingForMe_AndWhatIApproved()
        {
            var d = AddDoctor(); var me = (await _staff.EnsureContributorAsync(d.UserID, Ct))!;
            var author = AddWriter();
            var slug = await InReviewFor(me, author);

            var before = await Inbox.Handle(new GetHealthWikiInboxRequestModel { UserId = d.UserID }, Ct);
            Assert.That(before.Success, Is.True);
            Assert.That(before.Pending.Select(p => p.Slug), Is.EqualTo(new[] { slug }));
            Assert.That(before.Pending[0].AuthorName, Is.EqualTo("Author"));
            Assert.That(before.Pending[0].WaitingDays, Is.EqualTo(4));
            Assert.That(before.Published, Is.Empty);

            await SetNmcVerified(d); me = (await _staff.EnsureContributorAsync(d.UserID, Ct))!;
            var res = await Review.Handle(new ReviewHealthWikiArticleRequestModel { UserId = d.UserID, Slug = slug, Decision = "APPROVE", AccuracyConfirmed = true }, Ct);
            Assert.That(res.Outcome, Is.EqualTo("PUBLISHED"));

            var after = await Inbox.Handle(new GetHealthWikiInboxRequestModel { UserId = d.UserID }, Ct);
            Assert.That(after.Pending, Is.Empty);
            Assert.That(after.Published.Select(p => p.Slug), Is.EqualTo(new[] { slug }));
            Assert.That(after.Published[0].WaitingDays, Is.Null);
            Assert.That(after.Published[0].Views, Is.EqualTo(0));
        }

        [Test]
        public async Task Inbox_NeverShowsSomeoneElsesArticles()
        {
            var d = AddDoctor(); var other = AddDoctor("Dr. Other", "777");
            var mine = (await _staff.EnsureContributorAsync(d.UserID, Ct))!;
            var theirs = (await _staff.EnsureContributorAsync(other.UserID, Ct))!;
            await InReviewFor(theirs, AddWriter());

            var inbox = await Inbox.Handle(new GetHealthWikiInboxRequestModel { UserId = d.UserID }, Ct);
            Assert.That(inbox.Pending, Is.Empty);
            var detail = await Detail.Handle(new GetHealthWikiStaffArticleRequestModel { UserId = d.UserID, Slug = "high-blood-pressure" }, Ct);
            Assert.That(detail.StatusCode, Is.EqualTo(404));
        }

        [Test]
        public async Task Article_ShowsTheTextTheBadgeAndWhetherTheDecisionIsMine()
        {
            var d = AddDoctor(); var me = (await _staff.EnsureContributorAsync(d.UserID, Ct))!;
            me.Speciality = null;
            var slug = await InReviewFor(me, AddWriter());

            var res = await Detail.Handle(new GetHealthWikiStaffArticleRequestModel { UserId = d.UserID, Slug = slug }, Ct);

            var a = res.Article!;
            Assert.That(a.Status, Is.EqualTo("IN_REVIEW"));
            Assert.That(a.CanDecide, Is.True);
            Assert.That(a.Content, Is.EqualTo("## Know your numbers"));
            Assert.That(a.Reviewer.Name, Is.EqualTo("Dr. Meera Nair"));
            Assert.That(a.Reviewer.RegistrationNumber, Is.EqualTo("48213"));
            Assert.That(a.Reviewer.RegistrationCouncil, Is.EqualTo("Delhi Medical Council"));
            Assert.That(a.DailyViews, Is.Empty, "no chart while it is in review");
        }

        [Test]
        public async Task Article_ForAnApprovedLiveArticle_HasA14DayChart_AndNoDecision()
        {
            var d = AddDoctor(); var me = (await _staff.EnsureContributorAsync(d.UserID, Ct))!;
            var slug = await InReviewFor(me, AddWriter());
            await SetNmcVerified(d); await _staff.EnsureContributorAsync(d.UserID, Ct);
            await Review.Handle(new ReviewHealthWikiArticleRequestModel { UserId = d.UserID, Slug = slug, Decision = "APPROVE", AccuracyConfirmed = true }, Ct);
            var articleId = _context.HealthArticles.Single().ArticleId;
            _context.HealthArticleStatDailies.Add(new HealthArticleStatDaily { ArticleId = articleId, StatDate = DateTime.UtcNow.Date, Views = 94, Likes = 3 });
            _context.HealthArticleStatDailies.Add(new HealthArticleStatDaily { ArticleId = articleId, StatDate = DateTime.UtcNow.Date.AddDays(-13), Views = 10, Likes = 0 });
            _context.HealthArticleStatDailies.Add(new HealthArticleStatDaily { ArticleId = articleId, StatDate = DateTime.UtcNow.Date.AddDays(-14), Views = 999, Likes = 0 });
            await _context.SaveChangesAsync();

            var a = (await Detail.Handle(new GetHealthWikiStaffArticleRequestModel { UserId = d.UserID, Slug = slug }, Ct)).Article!;

            Assert.That(a.Status, Is.EqualTo("PUBLISHED"));
            Assert.That(a.CanDecide, Is.False);
            Assert.That(a.DailyViews, Has.Count.EqualTo(14));
            Assert.That(a.DailyViews[13], Is.EqualTo(94), "today is last");
            Assert.That(a.DailyViews[0], Is.EqualTo(10), "thirteen days ago is first");
            Assert.That(a.DailyViews.Sum(), Is.EqualTo(104), "day 14 is outside the chart");
        }

        // ------------------------------------------------------------------------------------------ the decision

        [Test]
        public async Task Review_ByAnUnverifiedHospitalDoctor_IsApprovedButWaitsForVerification()
        {
            var d = AddDoctor(); var me = (await _staff.EnsureContributorAsync(d.UserID, Ct))!;
            var slug = await InReviewFor(me, AddWriter());

            var res = await Review.Handle(new ReviewHealthWikiArticleRequestModel { UserId = d.UserID, Slug = slug, Decision = "APPROVE", AccuracyConfirmed = true }, Ct);

            Assert.That(res.Success, Is.True, res.Message);
            Assert.That(res.Outcome, Is.EqualTo("APPROVED_AWAITING_VERIFICATION"));
            Assert.That(_context.HealthArticles.Single().Status, Is.EqualTo("IN_REVIEW"));

            await SetNmcVerified(d);
            await _staff.SyncHospitalDoctorsAsync(null, Ct);
            Assert.That(_context.HealthArticles.Single().Status, Is.EqualTo("PUBLISHED"), "goes live when the CMS marks their registration as checked");
        }

        [Test]
        public async Task Review_RequestChanges_SendsItBack_AndNeedsAComment()
        {
            var d = AddDoctor(); var me = (await _staff.EnsureContributorAsync(d.UserID, Ct))!;
            var slug = await InReviewFor(me, AddWriter());

            var tooShort = await Review.Handle(new ReviewHealthWikiArticleRequestModel { UserId = d.UserID, Slug = slug, Decision = "REQUEST_CHANGES", Comment = "no" }, Ct);
            var ok = await Review.Handle(new ReviewHealthWikiArticleRequestModel { UserId = d.UserID, Slug = slug, Decision = "REQUEST_CHANGES", Comment = "Add when to see a doctor." }, Ct);

            Assert.That(tooShort.StatusCode, Is.EqualTo(400));
            Assert.That(ok.Outcome, Is.EqualTo("SENT_BACK"));
            Assert.That(_context.HealthArticles.Single().Status, Is.EqualTo("DRAFT"));
            Assert.That(_context.HealthArticles.Single().ReviewerComment, Is.EqualTo("Add when to see a doctor."));
        }

        [Test]
        public async Task Review_NeedsTheAccuracyConfirmation_AndTheRightDoctor()
        {
            var d = AddDoctor(); var me = (await _staff.EnsureContributorAsync(d.UserID, Ct))!;
            var other = AddDoctor("Dr. Other", "777");
            var slug = await InReviewFor(me, AddWriter());

            Assert.That((await Review.Handle(new ReviewHealthWikiArticleRequestModel { UserId = d.UserID, Slug = slug, Decision = "APPROVE", AccuracyConfirmed = false }, Ct)).StatusCode, Is.EqualTo(400));
            Assert.That((await Review.Handle(new ReviewHealthWikiArticleRequestModel { UserId = other.UserID, Slug = slug, Decision = "APPROVE", AccuracyConfirmed = true }, Ct)).StatusCode, Is.EqualTo(404));
            Assert.That((await Review.Handle(new ReviewHealthWikiArticleRequestModel { UserId = Guid.NewGuid(), Slug = slug, Decision = "APPROVE", AccuracyConfirmed = true }, Ct)).StatusCode, Is.EqualTo(403), "not a doctor");
            Assert.That(_context.HealthArticles.Single().Status, Is.EqualTo("IN_REVIEW"));
        }

        [Test]
        public async Task TheDoctorSeesTheSameArticleOnTheContributorPages()
        {
            // One doctor, both doors: the hospital doctor row is a contributor, so the Doctor Dekho pages show the same pending article.
            var d = AddDoctor(); var me = (await _staff.EnsureContributorAsync(d.UserID, Ct))!;
            var slug = await InReviewFor(me, AddWriter());

            var onPortal = await new GetContributorArticlesHandler(_context).Handle(new GetContributorArticlesRequestModel { ContributorId = me.ContributorId }, Ct);

            Assert.That(onPortal.Articles!.Pending.Select(p => p.Slug), Is.EqualTo(new[] { slug }));
        }

        // ------------------------------------------------------------------------------------------ readers: validation only (counters run on SQL Server)

        private HealthArticleStatsService Stats(string? secret = "a-test-secret-key-1234567890") => new(_context, new MemoryCache(new MemoryCacheOptions()),
            new ConfigurationBuilder().AddInMemoryCollection(new System.Collections.Generic.Dictionary<string, string?> { ["Jwt:SecretKey"] = secret }).Build());

        [Test]
        public async Task Reactions_NeedAVisitorKey_APublishedArticle_AndASecret()
        {
            _context.HealthArticles.Add(new HealthArticle { ArticleId = Guid.NewGuid(), Slug = "draft-one", Title = "T", Content = "c", Status = "DRAFT", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            await _context.SaveChangesAsync();

            Assert.That((await Stats().LikeAsync("draft-one", " ", Ct)).StatusCode, Is.EqualTo(400));
            Assert.That((await Stats().LikeAsync("draft-one", new string('x', 129), Ct)).StatusCode, Is.EqualTo(400));
            Assert.That((await Stats().ViewAsync("draft-one", "visitor-1", Ct)).StatusCode, Is.EqualTo(404), "a draft takes no reactions");
            Assert.That((await Stats().LikeAsync("nope", "visitor-1", Ct)).StatusCode, Is.EqualTo(404));
            Assert.That((await Stats(secret: "<set-via-env-Jwt-SecretKey>").LikeAsync("draft-one", "visitor-1", Ct)).StatusCode, Is.EqualTo(503));
        }

        [Test]
        public void VisitorKey_IsHashedPerArticle_AndNeverStoredRaw()
        {
            var article = Guid.NewGuid();
            var h = HealthArticleRules.HashVisitorKey("visitor-1", article, "secret");

            Assert.That(h, Has.Length.EqualTo(64));
            Assert.That(h, Does.Not.Contain("visitor-1"));
            Assert.That(HealthArticleRules.HashVisitorKey("visitor-1", article, "secret"), Is.EqualTo(h), "stable, so a repeat is recognised");
            Assert.That(HealthArticleRules.HashVisitorKey("visitor-1", Guid.NewGuid(), "secret"), Is.Not.EqualTo(h), "different per article");
            Assert.That(HealthArticleRules.HashVisitorKey("visitor-2", article, "secret"), Is.Not.EqualTo(h));
            Assert.That(HealthArticleRules.HashVisitorKey("visitor-1", article, "other"), Is.Not.EqualTo(h));
        }
    }
}
