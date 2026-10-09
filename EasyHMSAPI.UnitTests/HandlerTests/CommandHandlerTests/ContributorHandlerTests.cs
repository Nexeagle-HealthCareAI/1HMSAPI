using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class ContributorHandlerTests
    {
        private const string Mobile = "9891122002";
        private const string Secret = "test-secret-key-for-otp-hmac-1234567890";

        private AppDbContext _context = null!;
        private Mock<IWhatsAppMessagingService> _whatsApp = null!;
        private IConfiguration _config = null!;
        private string? _lastCode;
        private string? _lastInviteUrl;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _lastCode = null;
            _lastInviteUrl = null;
            _whatsApp = new Mock<IWhatsAppMessagingService>();
            _whatsApp.Setup(w => w.SendOtpAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string>((_, code) => _lastCode = code).ReturnsAsync(true);
            _whatsApp.Setup(w => w.SendHealthWikiInviteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string, string, string>((_, _, _, url) => _lastInviteUrl = url).ReturnsAsync(true);
            _config = new ConfigurationBuilder().AddInMemoryCollection(new System.Collections.Generic.Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = Secret,
                ["HealthWiki:ContributorBaseUrl"] = "https://doctordekho.example.com/",
            }).Build();
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private ContributorOtpSendHandler Send => new(_context, _whatsApp.Object, _config);
        private ContributorOtpVerifyHandler Verify => new(_context, _config);

        private HealthWikiContributor AddContributor(string type = "INDEPENDENT_DOCTOR", string status = "INVITED", string mobile = Mobile, string name = "Dr. Meera Nair")
        {
            var c = new HealthWikiContributor
            {
                ContributorId = Guid.NewGuid(), Type = type, FullName = name, Mobile = mobile, Status = status,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            _context.HealthWikiContributors.Add(c);
            _context.SaveChanges();
            return c;
        }

        private async Task<string> Code(string mobile = Mobile, string? inviteToken = null)
        {
            var res = await Send.Handle(new ContributorOtpSendRequestModel { Mobile = mobile, InviteToken = inviteToken }, CancellationToken.None);
            Assert.That(res.Success, Is.True, res.Message);
            return _lastCode!;
        }

        private async Task<string> InviteLink(HealthWikiContributor c, string role = "JOIN", HealthArticle? article = null)
        {
            var r = await ContributorInvites.CreateAndSendAsync(_context, _whatsApp.Object, _config, c, role, article, "Priya", CancellationToken.None);
            Assert.That(r.Created, Is.True, r.Error);
            return r.Url!.Split('/').Last();
        }

        // ------------------------------------------------------------------------------------------ pure rules

        [TestCase("9891122002", "9891122002")]
        [TestCase("+91 98911 22002", "9891122002")]
        [TestCase("919891122002", "9891122002")]
        [TestCase("09891122002", "9891122002")]
        [TestCase("5891122002", null)]
        [TestCase("98911", null)]
        [TestCase("", null)]
        [TestCase(null, null)]
        public void NormalizeMobile_AcceptsIndianNumbersOnly(string? input, string? expected) =>
            Assert.That(ContributorSecurity.NormalizeMobile(input), Is.EqualTo(expected));

        [Test]
        public void MaskMobile_NeverShowsTheWholeNumber()
        {
            var m = ContributorSecurity.MaskMobile(Mobile);
            Assert.That(m, Is.EqualTo("+91 98••• ••002"));
            Assert.That(m, Does.Not.Contain(Mobile));
        }

        [Test]
        public void Otp_HashesAreKeyedAndCompareInConstantTime()
        {
            var h = ContributorSecurity.HashOtp(Mobile, "123456", Secret);
            Assert.That(h, Has.Length.EqualTo(64));
            Assert.That(h, Does.Not.Contain("123456"));
            Assert.That(ContributorSecurity.OtpMatches(h, Mobile, "123456", Secret), Is.True);
            Assert.That(ContributorSecurity.OtpMatches(h, Mobile, "123457", Secret), Is.False);
            Assert.That(ContributorSecurity.OtpMatches(h, "9000000000", "123456", Secret), Is.False, "bound to the number");
            Assert.That(ContributorSecurity.OtpMatches(h, Mobile, "123456", "another-secret"), Is.False, "bound to the server secret");
            Assert.That(ContributorSecurity.NewOtpCode(), Does.Match("^[0-9]{6}$"));
        }

        [Test]
        public void Tokens_AreRandomUrlSafeAndHashedTo64Chars()
        {
            var a = ContributorSecurity.NewToken();
            var b = ContributorSecurity.NewToken();
            Assert.That(a, Is.Not.EqualTo(b));
            Assert.That(a, Does.Match("^[A-Za-z0-9_-]{43}$"));
            Assert.That(ContributorSecurity.HashToken(a), Has.Length.EqualTo(64));
        }

        // ------------------------------------------------------------------------------------------ send code

        [Test]
        public async Task SendOtp_StoresOnlyAHash_AndSendsTheCodeOnWhatsApp()
        {
            var res = await Send.Handle(new ContributorOtpSendRequestModel { Mobile = "+91 98911 22002" }, CancellationToken.None);

            Assert.That(res.Success, Is.True);
            Assert.That(res.Sent, Is.True);
            var otp = _context.ContributorOtps.Single();
            Assert.That(otp.Mobile, Is.EqualTo(Mobile));
            Assert.That(otp.CodeHash, Does.Not.Contain(_lastCode!));
            Assert.That(ContributorSecurity.OtpMatches(otp.CodeHash, Mobile, _lastCode!, Secret), Is.True);
            _whatsApp.Verify(w => w.SendOtpAsync(Mobile, _lastCode!), Times.Once);
        }

        [Test]
        public async Task SendOtp_BadNumber_Returns400_AndSendsNothing()
        {
            var res = await Send.Handle(new ContributorOtpSendRequestModel { Mobile = "12345" }, CancellationToken.None);

            Assert.That(res.StatusCode, Is.EqualTo(400));
            _whatsApp.Verify(w => w.SendOtpAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public async Task SendOtp_TooSoonAfterTheLastCode_Returns429WithRetryAfter()
        {
            await Code();
            var res = await Send.Handle(new ContributorOtpSendRequestModel { Mobile = Mobile }, CancellationToken.None);

            Assert.That(res.StatusCode, Is.EqualTo(429));
            Assert.That(res.RetryAfterSeconds, Is.InRange(1, 60));
            _whatsApp.Verify(w => w.SendOtpAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Test]
        public async Task SendOtp_AfterTheCooldown_CancelsThePreviousCode()
        {
            var first = await Code();
            _context.ContributorOtps.Single().CreatedAt = DateTime.UtcNow.AddMinutes(-2);
            await _context.SaveChangesAsync();
            var second = await Code();

            Assert.That(_context.ContributorOtps.Count(o => o.ConsumedAt == null), Is.EqualTo(1), "only the newest code is live");
            var old = await Verify.Handle(new ContributorOtpVerifyRequestModel { Mobile = Mobile, Code = first, Role = "WRITER" }, CancellationToken.None);
            if (first != second) Assert.That(old.StatusCode, Is.EqualTo(401));
        }

        [Test]
        public async Task SendOtp_FifthCodeInADay_IsTheLast()
        {
            for (var i = 0; i < 5; i++)
                _context.ContributorOtps.Add(new ContributorOtp
                {
                    OtpId = Guid.NewGuid(), Mobile = Mobile, CodeHash = new string('a', 64), ExpiresAt = DateTime.UtcNow.AddMinutes(-30),
                    ConsumedAt = DateTime.UtcNow.AddHours(-1), CreatedAt = DateTime.UtcNow.AddHours(-1 - i),
                });
            await _context.SaveChangesAsync();

            var res = await Send.Handle(new ContributorOtpSendRequestModel { Mobile = Mobile }, CancellationToken.None);

            Assert.That(res.StatusCode, Is.EqualTo(429));
            Assert.That(res.Message, Does.Contain("tomorrow"));
        }

        [Test]
        public async Task SendOtp_WhenWhatsAppFails_Returns502()
        {
            _whatsApp.Setup(w => w.SendOtpAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);
            var res = await Send.Handle(new ContributorOtpSendRequestModel { Mobile = Mobile }, CancellationToken.None);
            Assert.That(res.StatusCode, Is.EqualTo(502));
        }

        [Test]
        public async Task SendOtp_WithoutASecretConfigured_Returns503()
        {
            _config = new ConfigurationBuilder().AddInMemoryCollection(new System.Collections.Generic.Dictionary<string, string?> { ["Jwt:SecretKey"] = "<set-via-env-Jwt-SecretKey>" }).Build();
            var res = await Send.Handle(new ContributorOtpSendRequestModel { Mobile = Mobile }, CancellationToken.None);
            Assert.That(res.StatusCode, Is.EqualTo(503));
        }

        [Test]
        public async Task SendOtp_WithALink_OnlyGoesToTheNumberTheLinkWasSentTo()
        {
            var token = await InviteLink(AddContributor());

            var other = await Send.Handle(new ContributorOtpSendRequestModel { Mobile = "9000000001", InviteToken = token }, CancellationToken.None);
            var right = await Send.Handle(new ContributorOtpSendRequestModel { Mobile = Mobile, InviteToken = token }, CancellationToken.None);
            var bogus = await Send.Handle(new ContributorOtpSendRequestModel { Mobile = Mobile, InviteToken = "nope" }, CancellationToken.None);

            Assert.That(other.StatusCode, Is.EqualTo(403));
            Assert.That(right.Success, Is.True);
            Assert.That(bogus.StatusCode, Is.EqualTo(410));
            _whatsApp.Verify(w => w.SendOtpAsync("9000000001", It.IsAny<string>()), Times.Never);
        }

        [Test]
        public async Task SendOtp_DoesNotRevealWhetherTheNumberIsRegistered()
        {
            AddContributor();
            var known = await Send.Handle(new ContributorOtpSendRequestModel { Mobile = Mobile }, CancellationToken.None);
            var unknown = await Send.Handle(new ContributorOtpSendRequestModel { Mobile = "9000000002" }, CancellationToken.None);

            Assert.That(known.StatusCode, Is.EqualTo(unknown.StatusCode));
            Assert.That(known.Message, Is.EqualTo(unknown.Message));
        }

        // ------------------------------------------------------------------------------------------ verify

        [Test]
        public async Task Verify_SelfEnrol_CreatesTheContributorForTheChosenRole_AndASession()
        {
            var code = await Code();
            var res = await Verify.Handle(new ContributorOtpVerifyRequestModel { Mobile = Mobile, Code = code, Role = "health_worker" }, CancellationToken.None);

            Assert.That(res.Success, Is.True, res.Message);
            Assert.That(res.SignedIn, Is.True);
            Assert.That(res.NeedsRegistration, Is.True);
            var c = _context.HealthWikiContributors.Single();
            Assert.That(c.Type, Is.EqualTo("HEALTH_WORKER"));
            Assert.That(c.Status, Is.EqualTo("INVITED"));
            Assert.That(c.EnrolmentSource, Is.EqualTo("SELF_ENROLLED"));
            var session = _context.ContributorSessions.Single();
            Assert.That(session.TokenHash, Is.EqualTo(ContributorSecurity.HashToken(res.SessionToken!)));
            Assert.That(session.TokenHash, Is.Not.EqualTo(res.SessionToken), "the raw token is never stored");
            Assert.That(_context.ContributorOtps.Single().ConsumedAt, Is.Not.Null, "a code works once");
        }

        [Test]
        public async Task Verify_SelfEnrol_NeedsARoleFromTheAllowedThree()
        {
            var code = await Code();
            var none = await Verify.Handle(new ContributorOtpVerifyRequestModel { Mobile = Mobile, Code = code }, CancellationToken.None);
            var staff = await Verify.Handle(new ContributorOtpVerifyRequestModel { Mobile = Mobile, Code = code, Role = "STAFF" }, CancellationToken.None);
            var hospital = await Verify.Handle(new ContributorOtpVerifyRequestModel { Mobile = Mobile, Code = code, Role = "HOSPITAL_DOCTOR" }, CancellationToken.None);

            Assert.That(new[] { none.StatusCode, staff.StatusCode, hospital.StatusCode }, Is.All.EqualTo(400));
            Assert.That(_context.HealthWikiContributors.Count(), Is.EqualTo(0));
        }

        [Test]
        public async Task Verify_ReturningContributor_SignsInWithoutRegistrationWhenAlreadyRegistered()
        {
            AddContributor(status: "VERIFIED");
            var code = await Code();

            var res = await Verify.Handle(new ContributorOtpVerifyRequestModel { Mobile = Mobile, Code = code }, CancellationToken.None);

            Assert.That(res.Success, Is.True, res.Message);
            Assert.That(res.NeedsRegistration, Is.False);
            Assert.That(_context.HealthWikiContributors.Count(), Is.EqualTo(1));
        }

        [Test]
        public async Task Verify_WrongCode_Returns401_AndLocksTheCodeAfterFiveTries()
        {
            AddContributor(status: "VERIFIED");
            var code = await Code();
            var wrong = code == "111111" ? "222222" : "111111";

            for (var i = 0; i < 5; i++)
                Assert.That((await Verify.Handle(new ContributorOtpVerifyRequestModel { Mobile = Mobile, Code = wrong }, CancellationToken.None)).StatusCode, Is.EqualTo(401));
            var rightButLocked = await Verify.Handle(new ContributorOtpVerifyRequestModel { Mobile = Mobile, Code = code }, CancellationToken.None);

            Assert.That(rightButLocked.StatusCode, Is.EqualTo(401));
            Assert.That(_context.ContributorSessions.Count(), Is.EqualTo(0));
        }

        [Test]
        public async Task Verify_ExpiredOrUnknownCode_Returns401()
        {
            AddContributor(status: "VERIFIED");
            var code = await Code();
            _context.ContributorOtps.Single().ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await _context.SaveChangesAsync();

            var expired = await Verify.Handle(new ContributorOtpVerifyRequestModel { Mobile = Mobile, Code = code }, CancellationToken.None);
            var never = await Verify.Handle(new ContributorOtpVerifyRequestModel { Mobile = "9000000003", Code = "123456" }, CancellationToken.None);

            Assert.That(expired.StatusCode, Is.EqualTo(401));
            Assert.That(never.StatusCode, Is.EqualTo(401));
            Assert.That(expired.Message, Is.EqualTo(never.Message), "every failure reads the same");
        }

        [Test]
        public async Task Verify_RejectedContributor_CannotSignIn()
        {
            AddContributor(status: "REJECTED");
            var code = await Code();

            var res = await Verify.Handle(new ContributorOtpVerifyRequestModel { Mobile = Mobile, Code = code }, CancellationToken.None);

            Assert.That(res.StatusCode, Is.EqualTo(403));
            Assert.That(_context.ContributorSessions.Count(), Is.EqualTo(0));
        }

        [Test]
        public async Task Verify_WithAnInvitationLink_SignsInToTheInvitedPerson_AndUsesUpTheLink()
        {
            var invited = AddContributor(name: "Dr. Invited");
            var token = await InviteLink(invited);
            var code = await Code(inviteToken: token);

            var res = await Verify.Handle(new ContributorOtpVerifyRequestModel { Mobile = Mobile, Code = code, InviteToken = token }, CancellationToken.None);

            Assert.That(res.Success, Is.True, res.Message);
            Assert.That(res.NeedsRegistration, Is.True, "invited, profile not filled in yet");
            Assert.That(_context.ContributorSessions.Single().ContributorId, Is.EqualTo(invited.ContributorId));
            Assert.That(_context.HealthArticleAccessLinks.Single().UsedAt, Is.Not.Null);

            // the same link cannot start another sign-in
            var code2 = await CodeAfterCooldown(token);
            var again = await Verify.Handle(new ContributorOtpVerifyRequestModel { Mobile = Mobile, Code = code2, InviteToken = token }, CancellationToken.None);
            Assert.That(again.StatusCode, Is.EqualTo(410));
        }

        private async Task<string> CodeAfterCooldown(string? token)
        {
            foreach (var o in _context.ContributorOtps) o.CreatedAt = DateTime.UtcNow.AddMinutes(-5);
            await _context.SaveChangesAsync();
            var res = await Send.Handle(new ContributorOtpSendRequestModel { Mobile = Mobile }, CancellationToken.None);
            Assert.That(res.Success, Is.True, res.Message);
            return _lastCode!;
        }

        // ------------------------------------------------------------------------------------------ invite info

        [Test]
        public async Task InviteInfo_ShowsWhoInvitedWhy_AndOnlyAMaskedNumber()
        {
            var doctor = AddContributor(name: "Dr. Meera Nair");
            var article = new HealthArticle
            {
                ArticleId = Guid.NewGuid(), Slug = "high-blood-pressure", Title = "Living with High Blood Pressure", Content = "x",
                CoverImageUrl = "https://cdn.example.com/bp.jpg", CoverImageAlt = "alt", ReviewerContributorId = doctor.ContributorId,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            _context.HealthArticles.Add(article);
            await _context.SaveChangesAsync();
            var token = await InviteLink(doctor, "REVIEW", article);

            var info = await new GetContributorInviteHandler(_context).Handle(new GetContributorInviteRequestModel { Token = token }, CancellationToken.None);

            Assert.That(info.Success, Is.True);
            Assert.That(info.Kind, Is.EqualTo("REVIEW"));
            Assert.That(info.Role, Is.EqualTo("INDEPENDENT_DOCTOR"));
            Assert.That(info.InviterName, Is.EqualTo("Priya"));
            Assert.That(info.ArticleTitle, Is.EqualTo("Living with High Blood Pressure"));
            Assert.That(info.InviteeName, Is.EqualTo("Dr. Meera Nair"));
            Assert.That(info.MaskedMobile, Is.EqualTo("+91 98••• ••002"));
            Assert.That(System.Text.Json.JsonSerializer.Serialize(info), Does.Not.Contain(Mobile));
        }

        [Test]
        public async Task InviteInfo_UnknownUsedExpiredAndRevokedLinks()
        {
            var c = AddContributor();
            var token = await InviteLink(c);
            var handler = new GetContributorInviteHandler(_context);

            Assert.That((await handler.Handle(new GetContributorInviteRequestModel { Token = "nope" }, CancellationToken.None)).StatusCode, Is.EqualTo(404));
            Assert.That((await handler.Handle(new GetContributorInviteRequestModel { Token = null }, CancellationToken.None)).StatusCode, Is.EqualTo(404));

            var link = _context.HealthArticleAccessLinks.Single();
            link.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await _context.SaveChangesAsync();
            Assert.That((await handler.Handle(new GetContributorInviteRequestModel { Token = token }, CancellationToken.None)).StatusCode, Is.EqualTo(410));

            link.ExpiresAt = DateTime.UtcNow.AddDays(1); link.UsedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            Assert.That((await handler.Handle(new GetContributorInviteRequestModel { Token = token }, CancellationToken.None)).Message, Does.Contain("already been used"));

            link.UsedAt = null; link.RevokedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            Assert.That((await handler.Handle(new GetContributorInviteRequestModel { Token = token }, CancellationToken.None)).StatusCode, Is.EqualTo(410));
        }

        // ------------------------------------------------------------------------------------------ logout

        [Test]
        public async Task Logout_RevokesTheSession()
        {
            AddContributor(status: "VERIFIED");
            var code = await Code();
            await Verify.Handle(new ContributorOtpVerifyRequestModel { Mobile = Mobile, Code = code }, CancellationToken.None);
            var session = _context.ContributorSessions.Single();

            await new ContributorLogoutHandler(_context).Handle(new ContributorLogoutRequestModel { SessionId = session.SessionId }, CancellationToken.None);

            Assert.That(_context.ContributorSessions.Single().RevokedAt, Is.Not.Null);
        }

        // ------------------------------------------------------------------------------------------ CMS: invite and links

        private InviteContributorHandler Invite => new(_context, _whatsApp.Object, _config);
        private SendContributorLinkHandler SendLink => new(_context, _whatsApp.Object, _config);

        [Test]
        public async Task CmsInvite_CreatesTheContributor_AndSendsAJoinLink()
        {
            var res = await Invite.Handle(new InviteContributorRequestModel { FullName = " Dr. Asha ", Mobile = "98911 22002", Type = "independent_doctor", ActorName = "Priya" }, CancellationToken.None);

            Assert.That(res.Success, Is.True, res.Message);
            Assert.That(res.StatusCode, Is.EqualTo(201));
            Assert.That(res.LinkDelivered, Is.True);
            Assert.That(res.Link, Does.StartWith("https://doctordekho.example.com/contribute/invite/"));
            Assert.That(res.Contributor!.Status, Is.EqualTo("INVITED"));
            Assert.That(res.Contributor.EnrolmentSource, Is.EqualTo("INVITED"));
            Assert.That(res.Contributor.FullName, Is.EqualTo("Dr. Asha"));
            Assert.That(res.Contributor.LinkSentAt, Is.Not.Null);
            Assert.That(_lastInviteUrl, Is.EqualTo(res.Link));
            Assert.That(_context.HealthArticleAccessLinks.Single().TokenHash, Is.Not.Contain("contribute"));
        }

        [Test]
        public async Task CmsInvite_WhenWhatsAppDoesNotDeliver_StillReturnsTheLinkToCopy()
        {
            _whatsApp.Setup(w => w.SendHealthWikiInviteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);

            var res = await Invite.Handle(new InviteContributorRequestModel { FullName = "Asha", Mobile = Mobile, Type = "WRITER" }, CancellationToken.None);

            Assert.That(res.Success, Is.True);
            Assert.That(res.LinkDelivered, Is.False);
            Assert.That(res.Link, Is.Not.Null);
            Assert.That(res.Contributor!.LinkSentAt, Is.Null);
        }

        [Test]
        public async Task CmsInvite_Validation()
        {
            AddContributor();
            Assert.That((await Invite.Handle(new InviteContributorRequestModel { FullName = "", Mobile = "9000000001", Type = "WRITER" }, CancellationToken.None)).StatusCode, Is.EqualTo(400));
            Assert.That((await Invite.Handle(new InviteContributorRequestModel { FullName = "A", Mobile = "123", Type = "WRITER" }, CancellationToken.None)).StatusCode, Is.EqualTo(400));
            Assert.That((await Invite.Handle(new InviteContributorRequestModel { FullName = "A", Mobile = "9000000001", Type = "STAFF" }, CancellationToken.None)).StatusCode, Is.EqualTo(400));
            Assert.That((await Invite.Handle(new InviteContributorRequestModel { FullName = "A", Mobile = "9000000001", Type = "HOSPITAL_DOCTOR" }, CancellationToken.None)).StatusCode, Is.EqualTo(400));
            Assert.That((await Invite.Handle(new InviteContributorRequestModel { FullName = "A", Mobile = Mobile, Type = "WRITER" }, CancellationToken.None)).StatusCode, Is.EqualTo(409), "number already used");
        }

        [Test]
        public async Task CmsInvite_WithoutALinkAddressConfigured_Returns503_AndCreatesNothing()
        {
            _config = new ConfigurationBuilder().AddInMemoryCollection(new System.Collections.Generic.Dictionary<string, string?> { ["HealthWiki:ContributorBaseUrl"] = "<set-via-env-HealthWiki-ContributorBaseUrl>" }).Build();

            var res = await Invite.Handle(new InviteContributorRequestModel { FullName = "A", Mobile = "9000000001", Type = "WRITER" }, CancellationToken.None);

            Assert.That(res.StatusCode, Is.EqualTo(503));
            Assert.That(_context.HealthWikiContributors.Count(), Is.EqualTo(0));
        }

        [Test]
        public async Task CmsSendLink_ForAnArticle_IsTheReviewLinkForItsReviewer_AndTheWriteLinkForItsAuthor()
        {
            var doctor = AddContributor(status: "VERIFIED");
            var writer = AddContributor(type: "WRITER", mobile: "9000000009", name: "W", status: "VERIFIED");
            var article = new HealthArticle
            {
                ArticleId = Guid.NewGuid(), Slug = "a", Title = "T", Content = "c", Type = "MEDICAL", Status = "IN_REVIEW",
                ReviewerContributorId = doctor.ContributorId, AuthorContributorId = writer.ContributorId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            _context.HealthArticles.Add(article);
            await _context.SaveChangesAsync();

            var review = await SendLink.Handle(new SendContributorLinkRequestModel { ContributorId = doctor.ContributorId, ArticleSlug = "a" }, CancellationToken.None);
            var write = await SendLink.Handle(new SendContributorLinkRequestModel { ContributorId = writer.ContributorId, ArticleSlug = "a" }, CancellationToken.None);

            Assert.That(review.Success, Is.True, review.Message);
            Assert.That(write.Success, Is.True, write.Message);
            Assert.That(_context.HealthArticleAccessLinks.Select(l => l.Role).OrderBy(r => r), Is.EqualTo(new[] { "REVIEW", "WRITE" }));
        }

        [Test]
        public async Task CmsSendLink_ForAnArticleTheyAreNotOn_Returns400()
        {
            var c = AddContributor(status: "VERIFIED");
            _context.HealthArticles.Add(new HealthArticle { ArticleId = Guid.NewGuid(), Slug = "a", Title = "T", Content = "c", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            await _context.SaveChangesAsync();

            var res = await SendLink.Handle(new SendContributorLinkRequestModel { ContributorId = c.ContributorId, ArticleSlug = "a" }, CancellationToken.None);
            var unknown = await SendLink.Handle(new SendContributorLinkRequestModel { ContributorId = c.ContributorId, ArticleSlug = "zzz" }, CancellationToken.None);
            var nobody = await SendLink.Handle(new SendContributorLinkRequestModel { ContributorId = Guid.NewGuid() }, CancellationToken.None);

            Assert.That(res.StatusCode, Is.EqualTo(400));
            Assert.That(unknown.StatusCode, Is.EqualTo(404));
            Assert.That(nobody.StatusCode, Is.EqualTo(404));
        }

        [Test]
        public async Task CmsSendLink_ANewLinkReplacesTheOldUnusedOne()
        {
            var c = AddContributor();
            var first = await InviteLink(c);
            var res = await SendLink.Handle(new SendContributorLinkRequestModel { ContributorId = c.ContributorId }, CancellationToken.None);
            var second = res.Link!.Split('/').Last();

            Assert.That(res.Success, Is.True, res.Message);
            Assert.That((await new GetContributorInviteHandler(_context).Handle(new GetContributorInviteRequestModel { Token = first }, CancellationToken.None)).StatusCode, Is.EqualTo(410));
            Assert.That((await new GetContributorInviteHandler(_context).Handle(new GetContributorInviteRequestModel { Token = second }, CancellationToken.None)).StatusCode, Is.EqualTo(200));
        }

        [Test]
        public async Task CmsSendLink_NotForRejectedPeopleOrThoseWithoutANumber()
        {
            var rejected = AddContributor(status: "REJECTED");
            var hospital = AddContributor(type: "HOSPITAL_DOCTOR", mobile: null!, name: "Dr. H");

            Assert.That((await SendLink.Handle(new SendContributorLinkRequestModel { ContributorId = rejected.ContributorId }, CancellationToken.None)).StatusCode, Is.EqualTo(409));
            Assert.That((await SendLink.Handle(new SendContributorLinkRequestModel { ContributorId = hospital.ContributorId }, CancellationToken.None)).StatusCode, Is.EqualTo(409));
        }

        // ------------------------------------------------------------------------------------------ CMS: verify, reject, list

        [Test]
        public async Task CmsVerify_OnlyAfterRegistering_AndDoctorsNeedARegistration()
        {
            var invited = AddContributor(status: "INVITED");
            var noReg = AddContributor(status: "PENDING", mobile: "9000000004", name: "No Reg");
            var ok = AddContributor(status: "PENDING", mobile: "9000000005", name: "Ok");
            ok.RegistrationNumber = "48213"; ok.RegistrationCouncil = "Delhi Medical Council";
            var worker = AddContributor(type: "HEALTH_WORKER", status: "PENDING", mobile: "9000000006", name: "Worker");
            await _context.SaveChangesAsync();
            var h = new VerifyContributorHandler(_context);

            Assert.That((await h.Handle(new VerifyContributorRequestModel { ContributorId = invited.ContributorId }, CancellationToken.None)).StatusCode, Is.EqualTo(409));
            Assert.That((await h.Handle(new VerifyContributorRequestModel { ContributorId = noReg.ContributorId }, CancellationToken.None)).StatusCode, Is.EqualTo(400));
            var done = await h.Handle(new VerifyContributorRequestModel { ContributorId = ok.ContributorId, ActorName = "Priya" }, CancellationToken.None);
            Assert.That(done.Contributor!.Status, Is.EqualTo("VERIFIED"));
            Assert.That(_context.HealthWikiContributors.Single(c => c.ContributorId == ok.ContributorId).VerifiedBy, Is.EqualTo("Priya"));
            Assert.That((await h.Handle(new VerifyContributorRequestModel { ContributorId = ok.ContributorId }, CancellationToken.None)).StatusCode, Is.EqualTo(409));
            Assert.That((await h.Handle(new VerifyContributorRequestModel { ContributorId = worker.ContributorId }, CancellationToken.None)).Success, Is.True, "a health worker is approved without a registration");
        }

        [Test]
        public async Task CmsReject_NeedsAReason_SignsThePersonOut_AndCancelsTheirLinks()
        {
            var c = AddContributor(status: "VERIFIED");
            var token = await InviteLink(c);
            var code = await Code();
            await Verify.Handle(new ContributorOtpVerifyRequestModel { Mobile = Mobile, Code = code }, CancellationToken.None);
            var h = new RejectContributorHandler(_context);

            Assert.That((await h.Handle(new RejectContributorRequestModel { ContributorId = c.ContributorId }, CancellationToken.None)).StatusCode, Is.EqualTo(400));
            var res = await h.Handle(new RejectContributorRequestModel { ContributorId = c.ContributorId, Reason = "Registration not found." }, CancellationToken.None);

            Assert.That(res.Success, Is.True, res.Message);
            Assert.That(res.Contributor!.RejectReason, Is.EqualTo("Registration not found."));
            Assert.That(_context.ContributorSessions.All(s => s.RevokedAt != null), Is.True);
            Assert.That(_context.HealthArticleAccessLinks.All(l => l.RevokedAt != null), Is.True);
            Assert.That((await h.Handle(new RejectContributorRequestModel { ContributorId = c.ContributorId, Reason = "again" }, CancellationToken.None)).StatusCode, Is.EqualTo(409));
        }

        [Test]
        public async Task CmsList_LeavesOutPeopleWhoNeverFinishedJoining_AndFilters()
        {
            AddContributor(name: "Dr. A", status: "PENDING");
            AddContributor(type: "WRITER", mobile: "9000000007", name: "B", status: "VERIFIED");
            AddContributor(type: "WRITER", mobile: "9000000008", name: "", status: "INVITED");
            var h = new GetContributorsAdminHandler(_context);

            var all = await h.Handle(new GetContributorsAdminRequestModel(), CancellationToken.None);
            var pending = await h.Handle(new GetContributorsAdminRequestModel { Status = "pending" }, CancellationToken.None);
            var writers = await h.Handle(new GetContributorsAdminRequestModel { Type = "writer" }, CancellationToken.None);

            Assert.That(all.Contributors.Select(c => c.FullName), Is.EquivalentTo(new[] { "Dr. A", "B" }));
            Assert.That(pending.Contributors.Select(c => c.FullName), Is.EqualTo(new[] { "Dr. A" }));
            Assert.That(writers.Contributors.Select(c => c.FullName), Is.EqualTo(new[] { "B" }));
        }
    }
}
