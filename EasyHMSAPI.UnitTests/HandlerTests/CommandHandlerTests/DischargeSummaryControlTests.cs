using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Common;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.Extensions.Configuration;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    // Withdrawing a signature and the anonymous patient link both used to be unrestricted and untraceable.
    [TestFixture]
    public class DischargeSummaryControlTests
    {
        private AppDbContext _context = null!;
        private DischargeSummaryCommandHandlers _handler = null!;
        private DischargeSummaryDocumentCommandHandlers _documents = null!;
        private GetPublicDischargeSummaryPdfHandler _public = null!;
        private Guid _hospitalId;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _hospitalId = Guid.NewGuid();
            _handler = new DischargeSummaryCommandHandlers(_context);

            var blob = new Mock<IBlobStorageService>();
            blob.Setup(b => b.RefreshUrlAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("https://signed.example.com/d.pdf");
            var config = new Mock<IConfiguration>().Object;
            _documents = new DischargeSummaryDocumentCommandHandlers(_context, blob.Object, new Mock<IWhatsAppMessagingService>().Object, config);
            _public = new GetPublicDischargeSummaryPdfHandler(_context, blob.Object, config);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private DischargeSummary SeedSigned(DateTime? expiresAt = null, bool signed = true)
        {
            var summary = new DischargeSummary
            {
                DischargeSummaryId = Guid.NewGuid(), HospitalId = _hospitalId, AdmissionId = Guid.NewGuid(),
                IsSigned = signed, SignedAt = DateTime.UtcNow.AddHours(-2), SignedBy = "Dr Original", SignedByDoctorName = "Dr Original",
                AccessToken = "tok-old", AccessTokenExpiresAt = expiresAt ?? DateTime.UtcNow.AddDays(10),
                PdfBlobKey = "key", PdfUploadedAt = DateTime.UtcNow.AddHours(-2),
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            _context.DischargeSummary.Add(summary);
            _context.SaveChanges();
            return summary;
        }

        private Guid SeedAdmin()
        {
            var userId = HrAuthSeed.SeedMember(_context, _hospitalId);
            var roleId = Guid.NewGuid();
            _context.Roles.Add(new Role { RoleID = roleId, HospitalID = _hospitalId, RoleName = "Admin" });
            _context.UserRoles.Add(new UserRole { UserID = userId, RoleID = roleId });
            _context.SaveChanges();
            return userId;
        }

        private UnsignDischargeSummaryRequestModel Unsign(DischargeSummary s, Guid? caller, string? reason = "Wrong final diagnosis entered") => new()
        {
            HospitalId = s.HospitalId, AdmissionId = s.AdmissionId, LoggedInUserId = caller, LoggedInUserName = "Caller", Reason = reason,
        };

        [Test]
        public async Task Unsign_ByPlainMember_IsForbidden_AndSummaryStaysSigned()
        {
            var summary = SeedSigned();
            var member = HrAuthSeed.SeedMember(_context, _hospitalId);

            var response = await _handler.Handle(Unsign(summary, member), CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Forbidden, Is.True);
            Assert.That(_context.DischargeSummary.Single().IsSigned, Is.True);
            Assert.That(_context.DischargeSummaryAudit.Any(), Is.False);
        }

        [Test]
        public async Task Unsign_WithoutCaller_IsForbidden()
        {
            var summary = SeedSigned();
            var response = await _handler.Handle(Unsign(summary, null), CancellationToken.None);
            Assert.That(response.Forbidden, Is.True);
            Assert.That(_context.DischargeSummary.Single().IsSigned, Is.True);
        }

        [Test]
        public async Task Unsign_ByAdminOfAnotherHospital_IsForbidden()
        {
            var summary = SeedSigned();
            var otherAdmin = HrAuthSeed.SeedMember(_context, Guid.NewGuid());
            var response = await _handler.Handle(Unsign(summary, otherAdmin), CancellationToken.None);
            Assert.That(response.Forbidden, Is.True);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("oops")]
        public async Task Unsign_WithoutAProperReason_IsRefused(string? reason)
        {
            var summary = SeedSigned();
            var admin = SeedAdmin();

            var response = await _handler.Handle(Unsign(summary, admin, reason), CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Forbidden, Is.False);
            Assert.That(response.Message, Does.Contain("reason"));
            Assert.That(_context.DischargeSummary.Single().IsSigned, Is.True);
        }

        [Test]
        public async Task Unsign_ByAdmin_WithReason_AuditsKeepsPreviousSigner_AndKillsTheLink()
        {
            var summary = SeedSigned();
            var admin = SeedAdmin();

            var response = await _handler.Handle(Unsign(summary, admin), CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            var saved = _context.DischargeSummary.Single();
            Assert.That(saved.IsSigned, Is.False);
            Assert.That(saved.AccessToken, Is.Null);
            Assert.That(saved.AccessTokenExpiresAt, Is.Null);
            Assert.That(saved.PdfBlobKey, Is.Null);

            var audit = _context.DischargeSummaryAudit.Single();
            Assert.That(audit.Action, Is.EqualTo(DischargeSummaryAudit.ActionUnsign));
            Assert.That(audit.Reason, Is.EqualTo("Wrong final diagnosis entered"));
            Assert.That(audit.PreviousSignedBy, Is.EqualTo("Dr Original"));
            Assert.That(audit.PerformedByUserId, Is.EqualTo(admin));
        }

        [Test]
        public async Task Unsign_ByHolderOfDischargeUnsignPermission_IsAllowed()
        {
            var summary = SeedSigned();
            var user = HrAuthSeed.SeedMember(_context, _hospitalId, "discharge_unsign");

            var response = await _handler.Handle(Unsign(summary, user), CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
        }

        [Test]
        public async Task Sign_IsAudited()
        {
            var summary = SeedSigned(signed: false);

            var response = await _handler.Handle(new SignDischargeSummaryRequestModel
            {
                HospitalId = summary.HospitalId, AdmissionId = summary.AdmissionId,
                LoggedInUserId = Guid.NewGuid(), LoggedInUserName = "Dr S",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            Assert.That(_context.DischargeSummaryAudit.Single().Action, Is.EqualTo(DischargeSummaryAudit.ActionSign));
        }

        [Test]
        public async Task PublicLink_BeforeExpiry_Resolves()
        {
            SeedSigned(expiresAt: DateTime.UtcNow.AddDays(1));
            var response = await _public.Handle(new GetPublicDischargeSummaryPdfRequestModel { AccessToken = "tok-old" }, CancellationToken.None);
            Assert.That(response.Success, Is.True);
        }

        [Test]
        public async Task PublicLink_AfterExpiry_IsRefusedAsExpired()
        {
            SeedSigned(expiresAt: DateTime.UtcNow.AddMinutes(-1));
            var response = await _public.Handle(new GetPublicDischargeSummaryPdfRequestModel { AccessToken = "tok-old" }, CancellationToken.None);
            Assert.That(response.Success, Is.False);
            Assert.That(response.Expired, Is.True);
            Assert.That(response.RedirectUrl, Is.Null);
        }

        [Test]
        public async Task RegenerateLink_RotatesTheToken_RestartsValidity_AndIsAudited()
        {
            var summary = SeedSigned(expiresAt: DateTime.UtcNow.AddMinutes(-5));
            var user = Guid.NewGuid();

            var response = await _documents.Handle(new RegenerateDischargeLinkRequestModel
            {
                HospitalId = summary.HospitalId, AdmissionId = summary.AdmissionId, LoggedInUserId = user, LoggedInUserName = "Front desk", Reason = "QR shared by mistake",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            Assert.That(response.AccessToken, Is.Not.EqualTo("tok-old").And.Length.EqualTo(40));
            Assert.That(response.AccessTokenExpiresAt, Is.GreaterThan(DateTime.UtcNow.AddDays(DischargeLinkPolicy.DefaultValidityDays - 1)));

            // the old token no longer resolves at all; the new one does
            var old = await _public.Handle(new GetPublicDischargeSummaryPdfRequestModel { AccessToken = "tok-old" }, CancellationToken.None);
            Assert.That(old.Success, Is.False);
            var fresh = await _public.Handle(new GetPublicDischargeSummaryPdfRequestModel { AccessToken = response.AccessToken }, CancellationToken.None);
            Assert.That(fresh.Success, Is.True);

            var audit = _context.DischargeSummaryAudit.Single();
            Assert.That(audit.Action, Is.EqualTo(DischargeSummaryAudit.ActionLinkRegenerated));
            Assert.That(audit.Reason, Is.EqualTo("QR shared by mistake"));
        }

        [Test]
        public async Task SendWhatsApp_RefusesAnUnsignedSummary()
        {
            var summary = SeedSigned(signed: false);
            var response = await _documents.Handle(new SendDischargeSummaryWhatsAppRequestModel
            {
                HospitalId = summary.HospitalId, AdmissionId = summary.AdmissionId, MobileNumber = "9999999999",
            }, CancellationToken.None);
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("signed"));
        }

        [TestCase(null, 30)]
        [TestCase("7", 7)]
        [TestCase("0", 30)]
        [TestCase("garbage", 30)]
        public void LinkValidity_ReadsConfig_WithSafeDefault(string? configured, int expectedDays)
        {
            var cfg = new ConfigurationBuilder().AddInMemoryCollection(
                configured == null ? new System.Collections.Generic.Dictionary<string, string?>()
                                   : new System.Collections.Generic.Dictionary<string, string?> { ["DischargeSummary:PublicLinkValidityDays"] = configured }).Build();
            Assert.That(DischargeLinkPolicy.ValidityDays(cfg), Is.EqualTo(expectedDays));
        }
    }
}
