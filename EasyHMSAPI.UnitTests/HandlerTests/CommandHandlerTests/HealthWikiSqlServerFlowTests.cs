using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.Services.Implementations;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    /// <summary>
    /// The whole contributor journey against a REAL SQL Server that has the Health Wiki migrations applied, to catch what the in-memory
    /// provider cannot: SQL translation of the queries, the CHECK constraints and the unique indexes. It runs only when the environment
    /// variable HEALTHWIKI_SQL holds a connection string (for example to a throw-away SQL Server container); otherwise it is skipped.
    /// </summary>
    [TestFixture, Category("SqlServer")]
    public class HealthWikiSqlServerFlowTests
    {
        private AppDbContext _db = null!;
        private Mock<IWhatsAppMessagingService> _whatsApp = null!;
        private IConfiguration _config = null!;
        private string? _code;
        private CancellationToken Ct => CancellationToken.None;

        [SetUp]
        public void SetUp()
        {
            var cs = Environment.GetEnvironmentVariable("HEALTHWIKI_SQL");
            if (string.IsNullOrWhiteSpace(cs)) Assert.Ignore("HEALTHWIKI_SQL is not set.");
            _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(cs).Options);
            _whatsApp = new Mock<IWhatsAppMessagingService>();
            _whatsApp.Setup(w => w.SendOtpAsync(It.IsAny<string>(), It.IsAny<string>())).Callback<string, string>((_, c) => _code = c).ReturnsAsync(true);
            _whatsApp.Setup(w => w.SendHealthWikiInviteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
            _config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = "integration-test-secret-key-1234567890", ["HealthWiki:ContributorBaseUrl"] = "https://doctordekho.example.com",
            }).Build();
        }

        [TearDown]
        public void TearDown() => _db?.Dispose();

        private async Task<string> SignIn(string mobile, string? inviteToken = null, string? role = null)
        {
            // clear the 60-second cool-down between codes for this number
            foreach (var o in _db.ContributorOtps.Where(o => o.Mobile == mobile)) o.CreatedAt = DateTime.UtcNow.AddMinutes(-5);
            await _db.SaveChangesAsync();
            var send = await new ContributorOtpSendHandler(_db, _whatsApp.Object, _config).Handle(new ContributorOtpSendRequestModel { Mobile = mobile, InviteToken = inviteToken }, Ct);
            Assert.That(send.Success, Is.True, send.Message);
            var verify = await new ContributorOtpVerifyHandler(_db, _config).Handle(new ContributorOtpVerifyRequestModel { Mobile = mobile, Code = _code, InviteToken = inviteToken, Role = role }, Ct);
            Assert.That(verify.Success, Is.True, verify.Message);
            return verify.SessionToken!;
        }

        [Test]
        public async Task TheWholeJourney_OnSqlServer()
        {
            var tag = Guid.NewGuid().ToString("N")[..6];
            long seed = 9100000000 + Math.Abs(tag.GetHashCode() % 800000000);
            string author = seed.ToString(), reviewerMobile = (seed + 1).ToString();

            // CMS invites the doctor reviewer and the author joins by themselves.
            var invite = await new InviteContributorHandler(_db, _whatsApp.Object, _config).Handle(
                new InviteContributorRequestModel { FullName = "Dr. Reviewer " + tag, Mobile = reviewerMobile, Type = "INDEPENDENT_DOCTOR", ActorName = "Priya" }, Ct);
            Assert.That(invite.StatusCode, Is.EqualTo(201), invite.Message);
            var inviteToken = invite.Link!.Split('/').Last();
            Assert.That((await new GetContributorInviteHandler(_db).Handle(new GetContributorInviteRequestModel { Token = inviteToken }, Ct)).Success, Is.True);

            var reviewerSession = await SignIn(reviewerMobile, inviteToken);
            var reviewerId = _db.HealthWikiContributors.Single(c => c.Mobile == reviewerMobile).ContributorId;
            var reg = await new RegisterContributorHandler(_db).Handle(new RegisterContributorRequestModel
            {
                ContributorId = reviewerId, FullName = "Dr. Reviewer " + tag, Speciality = "Cardiologist", Qualification = "MBBS, MD",
                RegistrationNumber = "R" + tag, RegistrationCouncil = "Delhi Medical Council", RegistrationYear = "2012", Consent = true,
            }, Ct);
            Assert.That(reg.Profile!.Status, Is.EqualTo("PENDING"), reg.Message);
            Assert.That((await new VerifyContributorHandler(_db, new HealthWikiStaffService(_db)).Handle(new VerifyContributorRequestModel { ContributorId = reviewerId, ActorName = "Priya" }, Ct)).Success, Is.True);

            await SignIn(author, role: "INDEPENDENT_DOCTOR");
            var authorId = _db.HealthWikiContributors.Single(c => c.Mobile == author).ContributorId;
            await new RegisterContributorHandler(_db).Handle(new RegisterContributorRequestModel
            {
                ContributorId = authorId, FullName = "Dr. Author " + tag, Speciality = "GP", Qualification = "MBBS",
                RegistrationNumber = "A" + tag, RegistrationCouncil = "Delhi Medical Council", RegistrationYear = "2015", Consent = true,
            }, Ct);
            await new VerifyContributorHandler(_db, new HealthWikiStaffService(_db)).Handle(new VerifyContributorRequestModel { ContributorId = authorId }, Ct);

            // Author writes, CMS assigns the reviewer, author submits, reviewer approves: the article is live.
            var created = await new SaveContributorArticleHandler(_db, new HealthArticleService(_db)).Handle(new SaveContributorArticleRequestModel
            { ContributorId = authorId, Type = "MEDICAL", Title = "BP " + tag, Description = "d", Content = "## Hello" }, Ct);
            Assert.That(created.StatusCode, Is.EqualTo(201), created.Message);
            var slug = created.Article!.Slug;
            var assign = await new SaveHealthArticleHandler(new HealthArticleService(_db)).Handle(new SaveHealthArticleRequestModel { Slug = slug, ReviewerContributorId = reviewerId, ActorName = "Priya" }, Ct);
            Assert.That(assign.Success, Is.True, assign.Message);
            Assert.That((await new SubmitContributorArticleHandler(_db).Handle(new SubmitContributorArticleRequestModel { ContributorId = authorId, Slug = slug }, Ct)).Success, Is.True);

            var pending = (await new GetContributorArticlesHandler(_db).Handle(new GetContributorArticlesRequestModel { ContributorId = reviewerId }, Ct)).Articles!;
            Assert.That(pending.Pending.Select(a => a.Slug), Does.Contain(slug));
            var approved = await new ReviewContributorArticleHandler(_db, new HealthArticleReviewService(_db)).Handle(new ReviewContributorArticleRequestModel
            { ContributorId = reviewerId, Slug = slug, Decision = "APPROVE", AccuracyConfirmed = true }, Ct);
            Assert.That(approved.Success, Is.True, approved.Message);

            var pub = (await new GetPublicHealthArticlesHandler(_db).Handle(new GetPublicHealthArticlesRequestModel { Slug = slug }, Ct)).Articles.Single();
            Assert.That(pub.Reviewer!.FullName, Is.EqualTo("Dr. Reviewer " + tag));
            Assert.That(pub.Reviewer.IsRegistrationVerified, Is.True);
            Assert.That(pub.Author!.FullName, Is.EqualTo("Dr. Author " + tag));

            // An edit to the live article stays pending; the live text is untouched.
            await new SaveContributorArticleHandler(_db, new HealthArticleService(_db)).Handle(new SaveContributorArticleRequestModel
            { ContributorId = authorId, Slug = slug, Type = "MEDICAL", Title = "BP edited " + tag, Content = "## Edited" }, Ct);
            Assert.That((await new GetPublicHealthArticlesHandler(_db).Handle(new GetPublicHealthArticlesRequestModel { Slug = slug }, Ct)).Articles.Single().Title, Is.EqualTo("BP " + tag));
            var admin = (await new GetHealthArticlesAdminHandler(_db).Handle(new GetHealthArticlesAdminRequestModel { Slug = slug }, Ct)).Articles.Single();
            Assert.That(admin.HasPendingRevision, Is.True);

            // Topics, the CMS inbox and the tab counts.
            var topic = await new CreateContributorTopicHandler(_db).Handle(new CreateContributorTopicRequestModel
            { ContributorId = authorId, Title = "Topic " + tag, Type = "MEDICAL", Outline = "o", WhyItMatters = "w" }, Ct);
            Assert.That(topic.StatusCode, Is.EqualTo(201), topic.Message);
            var accepted = await new DecideTopicHandler(_db).Handle(new DecideTopicRequestModel { TopicId = topic.Topic!.Id, Action = "ACCEPT", ActorName = "Priya" }, Ct);
            Assert.That(accepted.Success, Is.True, accepted.Message);
            Assert.That((await new GetTopicRequestsAdminHandler(_db).Handle(new GetTopicRequestsAdminRequestModel(), Ct)).Topics!.Select(t => t.TopicId), Does.Contain(topic.Topic.Id));
            var summary = await new GetHealthWikiSummaryHandler(_db).Handle(new GetHealthWikiSummaryRequestModel(), Ct);
            Assert.That(summary.PendingContributors, Is.GreaterThanOrEqualTo(0));
            Assert.That((await new GetHealthArticleHistoryHandler(_db).Handle(new GetHealthArticleHistoryRequestModel { Slug = slug }, Ct)).Entries, Is.Not.Empty);
            Assert.That((await new GetContributorsAdminHandler(_db, new HealthWikiStaffService(_db)).Handle(new GetContributorsAdminRequestModel(), Ct)).Contributors.Select(c => c.Mobile), Does.Contain(author));

            // The same number cannot be enrolled twice (unique index), and the sessions are stored hashed.
            Assert.That((await new InviteContributorHandler(_db, _whatsApp.Object, _config).Handle(
                new InviteContributorRequestModel { FullName = "Dup", Mobile = author, Type = "WRITER" }, Ct)).StatusCode, Is.EqualTo(409));
            Assert.That(_db.ContributorSessions.Any(s => s.TokenHash == reviewerSession), Is.False);
            var conditions = await new GetHealthWikiConditionsHandler(_db, _config).Handle(new GetHealthWikiConditionsRequestModel(), Ct);
            Assert.That(conditions.Conditions, Does.Contain("diabetes"));
        }

        [Test]
        public async Task HospitalDoctorReview_AndReaderCounters_OnSqlServer()
        {
            var tag = Guid.NewGuid().ToString("N")[..6];
            var userId = Guid.NewGuid();
            // A hospital doctor as EasyHMS holds them. The test database has the core tables but not the seed data, so the foreign keys to
            // statuses and hospitals are switched off for these three rows.
            await _db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.Users NOCHECK CONSTRAINT ALL; ALTER TABLE dbo.UserProfiles NOCHECK CONSTRAINT ALL; ALTER TABLE dbo.Doctors NOCHECK CONSTRAINT ALL;");
            await _db.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT dbo.Users(UserID, MobileNumber, UserStatusId) VALUES({userId}, {(8000000000L + Math.Abs(tag.GetHashCode() % 900000000)).ToString()}, 1);
                INSERT dbo.UserProfiles(UserID, FullName, UserStatusId) VALUES({userId}, {"Dr. Hospital " + tag}, 1);
                INSERT dbo.Doctors(DoctorID, HospitalID, UserID, LicenseNumber, Qualification, MedicalCouncil, RegistrationYear)
                VALUES({Guid.NewGuid()}, {Guid.NewGuid()}, {userId}, {"H" + tag}, {"MBBS, MD"}, {"Delhi Medical Council"}, 2011);");

            var staff = new HealthWikiStaffService(_db);
            var me = await staff.EnsureContributorAsync(userId, Ct);
            Assert.That(me!.Status, Is.EqualTo("PENDING"));
            Assert.That((await staff.EnsureContributorAsync(userId, Ct))!.ContributorId, Is.EqualTo(me.ContributorId), "one row per doctor");
            // The CMS has not marked this doctor's registration yet, so the Health Wiki's verify button points to the doctor list.
            Assert.That((await new VerifyContributorHandler(_db, new HealthWikiStaffService(_db)).Handle(new VerifyContributorRequestModel { ContributorId = me.ContributorId }, Ct)).StatusCode, Is.EqualTo(409));
            await _db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.Doctors SET IsRegistrationVerified = 1, RegistrationVerifiedAt = SYSUTCDATETIME() WHERE UserID = {userId}");
            Assert.That((await staff.EnsureContributorAsync(userId, Ct))!.Status, Is.EqualTo("VERIFIED"), "follows the Doctors flag");

            // A writer (sector update) is not allowed to author medical, so the author is an independent doctor.
            var author = new EasyHMSAPI.Domain.Entities.HealthWikiContributor
            {
                ContributorId = Guid.NewGuid(), Type = "INDEPENDENT_DOCTOR", FullName = "Dr. Author " + tag, Mobile = (7000000000L + Math.Abs(tag.GetHashCode() % 900000000)).ToString(),
                Status = "VERIFIED", RegistrationNumber = "A" + tag, RegistrationCouncil = "MCI", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            _db.HealthWikiContributors.Add(author);
            await _db.SaveChangesAsync();
            var created = await new SaveContributorArticleHandler(_db, new HealthArticleService(_db)).Handle(new SaveContributorArticleRequestModel
            { ContributorId = author.ContributorId, Type = "MEDICAL", Title = "Hospital review " + tag, Content = "## Body" }, Ct);
            var slug = created.Article!.Slug;
            Assert.That((await new HealthArticleService(_db).SaveAsync(new SaveHealthArticleRequestModel { Slug = slug, ReviewerContributorId = me.ContributorId }, Ct)).Success, Is.True);
            Assert.That((await new SubmitContributorArticleHandler(_db).Handle(new SubmitContributorArticleRequestModel { ContributorId = author.ContributorId, Slug = slug }, Ct)).Success, Is.True);

            var inbox = await new GetHealthWikiInboxHandler(_db, staff).Handle(new GetHealthWikiInboxRequestModel { UserId = userId }, Ct);
            Assert.That(inbox.Pending.Select(p => p.Slug), Does.Contain(slug));
            var detail = await new GetHealthWikiStaffArticleHandler(_db, staff).Handle(new GetHealthWikiStaffArticleRequestModel { UserId = userId, Slug = slug }, Ct);
            Assert.That(detail.Article!.CanDecide, Is.True);
            var review = await new ReviewHealthWikiArticleHandler(staff, new HealthArticleReviewService(_db)).Handle(
                new ReviewHealthWikiArticleRequestModel { UserId = userId, Slug = slug, Decision = "APPROVE", AccuracyConfirmed = true }, Ct);
            Assert.That(review.Outcome, Is.EqualTo("PUBLISHED"), review.Message);

            // Readers: one Helpful per visitor, one view per visitor per day, totals and today's chart bar kept together.
            var stats = new HealthArticleStatsService(_db, new MemoryCache(new MemoryCacheOptions()), _config);
            Assert.That((await stats.LikeAsync(slug, "visitor-a", Ct)).Changed, Is.True);
            Assert.That((await stats.LikeAsync(slug, "visitor-a", Ct)).Changed, Is.False, "the same reader cannot like twice");
            var liked = await stats.LikeAsync(slug, "visitor-b", Ct);
            Assert.That(liked.Likes, Is.EqualTo(2));
            Assert.That((await stats.ViewAsync(slug, "visitor-a", Ct)).Changed, Is.True);
            Assert.That((await stats.ViewAsync(slug, "visitor-a", Ct)).Changed, Is.False, "a reload the same day is not a new view");
            var viewed = await stats.ViewAsync(slug, "visitor-b", Ct);
            Assert.That(viewed.Views, Is.EqualTo(2));
            Assert.That(viewed.Likes, Is.EqualTo(2));
            Assert.That(_db.HealthArticleLikes.Count(l => l.ArticleId == _db.HealthArticles.First(a => a.Slug == slug).ArticleId), Is.EqualTo(2));
            Assert.That(_db.HealthArticleLikes.Any(l => l.VisitorKeyHash.Contains("visitor")), Is.False, "the visitor key is stored hashed");

            var after = await new GetHealthWikiStaffArticleHandler(_db, staff).Handle(new GetHealthWikiStaffArticleRequestModel { UserId = userId, Slug = slug }, Ct);
            Assert.That(after.Article!.DailyViews, Has.Count.EqualTo(14));
            Assert.That(after.Article.DailyViews[13], Is.EqualTo(2));
            Assert.That(after.Article.Views, Is.EqualTo(2));
            Assert.That(after.Article.Likes, Is.EqualTo(2));
        }
    }
}
