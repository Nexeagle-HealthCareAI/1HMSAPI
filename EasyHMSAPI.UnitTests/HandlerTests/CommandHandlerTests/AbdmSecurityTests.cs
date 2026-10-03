using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Application.Services.Models;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    // A-01 (linking trusted whatever the client sent), A-02 (consent only a checkbox) and A-03 (callback gated only by a URL secret).
    [TestFixture]
    public class AbdmSecurityTests
    {
        private AppDbContext _context = null!;
        private Mock<IAbdmAbhaService> _abha = null!;
        private IAbhaLinkProofStore _proofs = null!;
        private Guid _hospitalId, _staff, _otherStaff;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _abha = new Mock<IAbdmAbhaService>();
            _proofs = new AbhaLinkProofStore(new MemoryCache(new MemoryCacheOptions()));
            _hospitalId = Guid.NewGuid();
            _staff = HrAuthSeed.SeedMember(_context, _hospitalId);
            _otherStaff = HrAuthSeed.SeedMember(_context, _hospitalId);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        // ---------------------------------------------------------------- A-02 consent

        private async Task<Guid> RecordConsent(Guid? caller = null, string givenBy = "PATIENT")
        {
            var response = await new RecordAbhaConsentHandler(_context).Handle(new RecordAbhaConsentRequestModel
            {
                HospitalId = _hospitalId, CallerUserId = caller ?? _staff, ConsentVersion = AbhaConsentRules.Version, GivenBy = givenBy, SubjectName = "Asha", LoggedInUserName = "Front desk",
            }, CancellationToken.None);
            Assert.That(response.Success, Is.True, response.Message);
            return response.ConsentId!.Value;
        }

        private GenerateAadhaarOtpHandler OtpHandler() => new(_abha.Object, _context);

        private GenerateAadhaarOtpRequestModel OtpRequest(Guid? consentId, Guid? caller = null, Guid? hospital = null) => new()
        {
            HospitalId = hospital ?? _hospitalId, AadhaarNumber = "123456789012", ConsentId = consentId, CallerUserId = caller ?? _staff,
        };

        [Test]
        public async Task RecordingConsent_StoresTheWordingVersionAndHash_AsEvidence()
        {
            var id = await RecordConsent(givenBy: "guardian");

            var saved = _context.AbhaConsent.Single(c => c.AbhaConsentId == id);
            Assert.That(saved.GrantedByUserId, Is.EqualTo(_staff));
            Assert.That(saved.HospitalId, Is.EqualTo(_hospitalId));
            Assert.That(saved.GivenBy, Is.EqualTo("GUARDIAN"));
            Assert.That(saved.ConsentVersion, Is.EqualTo(AbhaConsentRules.Version));
            Assert.That(saved.ConsentTextSnapshot, Is.EqualTo(AbhaConsentRules.Text));
            Assert.That(saved.ConsentTextSha256, Is.EqualTo(AbhaConsentRules.TextSha256()));
        }

        [Test]
        public async Task RecordingConsent_RefusesANonMember_AStaleVersion_AndAnUnstatedGiver()
        {
            var handler = new RecordAbhaConsentHandler(_context);
            RecordAbhaConsentRequestModel Req(Guid caller, string? version = null, string? givenBy = "PATIENT") => new()
            { HospitalId = _hospitalId, CallerUserId = caller, ConsentVersion = version ?? AbhaConsentRules.Version, GivenBy = givenBy };

            Assert.That((await handler.Handle(Req(Guid.NewGuid()), CancellationToken.None)).Success, Is.False, "not a member of the hospital");
            Assert.That((await handler.Handle(Req(_staff, version: "0.9"), CancellationToken.None)).Message, Does.Contain("out of date"));
            Assert.That((await handler.Handle(Req(_staff, givenBy: null), CancellationToken.None)).Message, Does.Contain("patient or a guardian"));
            Assert.That(_context.AbhaConsent.Any(), Is.False);
        }

        [Test]
        public async Task Otp_WithoutConsent_IsRefused_AndAbdmIsNeverCalled()
        {
            var response = await OtpHandler().Handle(OtpRequest(null), CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("consent"));
            _abha.Verify(a => a.GenerateAadhaarOtpAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task Otp_WithValidConsent_Proceeds_AndTheConsentRecordsTheUse()
        {
            _abha.Setup(a => a.GenerateAadhaarOtpAsync("123456789012", It.IsAny<CancellationToken>())).ReturnsAsync(new AbdmOtpTxnResult { TxnId = "txn-1" });
            var id = await RecordConsent();

            var response = await OtpHandler().Handle(OtpRequest(id), CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            var saved = _context.AbhaConsent.Single();
            Assert.That(saved.OtpRequestCount, Is.EqualTo(1));
            Assert.That(saved.TxnId, Is.EqualTo("txn-1"));
            Assert.That(saved.LastOtpRequestedAt, Is.Not.Null);
        }

        [Test]
        public async Task Otp_ConsentOfAnotherUserOrHospital_IsRefused()
        {
            var id = await RecordConsent(caller: _staff);

            var otherUser = await OtpHandler().Handle(OtpRequest(id, caller: _otherStaff), CancellationToken.None);
            var otherHospital = await OtpHandler().Handle(OtpRequest(id, hospital: Guid.NewGuid()), CancellationToken.None);
            var unknown = await OtpHandler().Handle(OtpRequest(Guid.NewGuid()), CancellationToken.None);

            Assert.That(otherUser.Success, Is.False);
            Assert.That(otherHospital.Success, Is.False);
            Assert.That(unknown.Success, Is.False);
            Assert.That(otherUser.Message, Is.EqualTo(unknown.Message), "ids must not be probeable");
            _abha.Verify(a => a.GenerateAadhaarOtpAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task Otp_ExpiredConsent_IsRefused()
        {
            var id = await RecordConsent();
            _context.AbhaConsent.Single().CreatedAt = DateTime.UtcNow.AddMinutes(-31);
            _context.SaveChanges();

            var response = await OtpHandler().Handle(OtpRequest(id), CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("expired"));
        }

        [Test]
        public async Task Otp_IsCappedAtThreePerConsent_FirstOtpPlusTwoResends()
        {
            _abha.Setup(a => a.GenerateAadhaarOtpAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new AbdmOtpTxnResult { TxnId = "t" });
            var id = await RecordConsent();

            for (var i = 0; i < 3; i++)
                Assert.That((await OtpHandler().Handle(OtpRequest(id), CancellationToken.None)).Success, Is.True, $"request {i + 1}");
            var fourth = await OtpHandler().Handle(OtpRequest(id), CancellationToken.None);

            Assert.That(fourth.Success, Is.False);
            Assert.That(fourth.Message, Does.Contain("limit"));
            _abha.Verify(a => a.GenerateAadhaarOtpAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        }

        [Test]
        public async Task Otp_AFailingAbdmCall_StillCountsTheAttempt()
        {
            _abha.Setup(a => a.GenerateAadhaarOtpAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("ABDM down"));
            var id = await RecordConsent();

            var response = await OtpHandler().Handle(OtpRequest(id), CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(_context.AbhaConsent.Single().OtpRequestCount, Is.EqualTo(1));
        }

        // ---------------------------------------------------------------- A-01 server-verified linking

        private AbdmProfileResult AbdmProfile(string number = "91-1234-5678-9012") => new()
        {
            TxnId = "session-1", AbhaNumber = number, AbhaAddress = "asha@abdm", FullName = "Asha Rao", Gender = "F", DateOfBirth = "1990-01-01", Mobile = "9876543210",
        };

        private async Task<string> VerifyLogin(Guid? caller = null, string number = "91-1234-5678-9012")
        {
            _abha.Setup(a => a.VerifyLoginOtpAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(AbdmProfile(number));
            var response = await new VerifyAbdmLoginOtpHandler(_abha.Object, _proofs).Handle(new VerifyAbdmLoginOtpRequestModel
            {
                HospitalId = _hospitalId, TxnId = "t", Otp = "123456", CallerUserId = caller ?? _staff,
            }, CancellationToken.None);
            Assert.That(response.Success, Is.True, response.Message);
            Assert.That(response.LinkToken, Is.Not.Null.And.Not.Empty);
            return response.LinkToken!;
        }

        private SaveLinkedAbhaAccountHandler LinkHandler() => new(_context, _proofs);

        private SaveLinkedAbhaAccountRequestModel LinkRequest(string? token, Guid? caller = null, Guid? hospital = null) => new()
        {
            HospitalId = hospital ?? _hospitalId, LinkToken = token, CallerUserId = caller ?? _staff, LoggedInUserName = "Front desk",
            // what a forging client would send; none of it may be stored
            AbhaNumber = "99-9999-9999-9999", FullName = "Someone Else", Mobile = "0000000000",
        };

        [Test]
        public async Task Link_StoresWhatAbdmReturned_NotWhatTheClientSent()
        {
            var token = await VerifyLogin();

            var response = await LinkHandler().Handle(LinkRequest(token), CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            var saved = _context.AbhaAccount.Single();
            Assert.That(saved.AbhaNumber, Is.EqualTo("91-1234-5678-9012"));
            Assert.That(saved.FullName, Is.EqualTo("Asha Rao"));
            Assert.That(saved.Mobile, Is.EqualTo("9876543210"));
            Assert.That(_context.AbhaAccount.Any(a => a.AbhaNumber == "99-9999-9999-9999"), Is.False);
        }

        [Test]
        public async Task Link_WithoutAToken_IsRefused_SoAForgedCallRecordsNothing()
        {
            var noToken = await LinkHandler().Handle(LinkRequest(null), CancellationToken.None);
            var madeUp = await LinkHandler().Handle(LinkRequest("not-a-real-token"), CancellationToken.None);

            Assert.That(noToken.Success, Is.False);
            Assert.That(madeUp.Success, Is.False);
            Assert.That(_context.AbhaAccount.Any(), Is.False);
        }

        [Test]
        public async Task Link_TokenWorksOnce()
        {
            var token = await VerifyLogin();
            Assert.That((await LinkHandler().Handle(LinkRequest(token), CancellationToken.None)).Success, Is.True);

            var replay = await LinkHandler().Handle(LinkRequest(token), CancellationToken.None);

            Assert.That(replay.Success, Is.False);
            Assert.That(_context.AbhaAccount.Count(), Is.EqualTo(1));
        }

        [Test]
        public async Task Link_TokenIsBoundToTheUserAndHospitalThatVerified()
        {
            var token = await VerifyLogin(caller: _staff);

            var otherUser = await LinkHandler().Handle(LinkRequest(token, caller: _otherStaff), CancellationToken.None);
            Assert.That(otherUser.Success, Is.False, "somebody else cannot use the token");

            var otherHospital = Guid.NewGuid();
            var outsider = HrAuthSeed.SeedMember(_context, otherHospital);
            var wrongHospital = await LinkHandler().Handle(LinkRequest(token, caller: outsider, hospital: otherHospital), CancellationToken.None);
            Assert.That(wrongHospital.Success, Is.False);

            // a failed attempt by someone else must not burn the legitimate user's token
            var rightful = await LinkHandler().Handle(LinkRequest(token, caller: _staff), CancellationToken.None);
            Assert.That(rightful.Success, Is.True, rightful.Message);
        }

        [Test]
        public async Task Link_ByANonMember_IsRefused()
        {
            var token = await VerifyLogin();
            var response = await LinkHandler().Handle(LinkRequest(token, caller: Guid.NewGuid()), CancellationToken.None);
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("access"));
        }

        [Test]
        public async Task Link_ExistingAccount_IsRefreshedFromAbdm()
        {
            _context.AbhaAccount.Add(new AbhaAccount { AbhaAccountId = Guid.NewGuid(), HospitalId = _hospitalId, AbhaNumber = "91-1234-5678-9012", FullName = "Old Name", CreatedAt = DateTime.UtcNow });
            _context.SaveChanges();
            var token = await VerifyLogin();

            var response = await LinkHandler().Handle(LinkRequest(token), CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(_context.AbhaAccount.Single().FullName, Is.EqualTo("Asha Rao"));
        }

        [Test]
        public void ProofStore_UnknownAndBlankTokens_AreNull()
        {
            Assert.That(_proofs.Consume(null, _staff, _hospitalId), Is.Null);
            Assert.That(_proofs.Consume("  ", _staff, _hospitalId), Is.Null);
            Assert.That(_proofs.Consume("nope", _staff, _hospitalId), Is.Null);
        }

        // ---------------------------------------------------------------- A-03 callback guard

        private static AbdmCallbackGuard Guard(params (string Key, string Value)[] settings) =>
            new(new ConfigurationBuilder().AddInMemoryCollection(settings.ToDictionary(s => s.Key, s => (string?)s.Value)).Build(),
                new MemoryCache(new MemoryCacheOptions()), new Mock<IHttpClientFactory>().Object);

        [Test]
        public async Task CallbackGuard_WithNothingConfigured_BehavesAsBefore()
        {
            var (allowed, _) = await Guard().CheckAsync(IPAddress.Parse("1.2.3.4"), null, CancellationToken.None);
            Assert.That(allowed, Is.True);
        }

        [Test]
        public async Task CallbackGuard_IpAllowList_OnlyServesListedAddresses()
        {
            var guard = Guard(("Abdm:CallbackAllowedIps", "10.0.0.5, 20.30.40.50"));

            Assert.That((await guard.CheckAsync(IPAddress.Parse("20.30.40.50"), null, CancellationToken.None)).Allowed, Is.True);
            Assert.That((await guard.CheckAsync(IPAddress.Parse("::ffff:10.0.0.5"), null, CancellationToken.None)).Allowed, Is.True, "IPv4-mapped IPv6 form");
            Assert.That((await guard.CheckAsync(IPAddress.Parse("9.9.9.9"), null, CancellationToken.None)).Allowed, Is.False);
            Assert.That((await guard.CheckAsync(null, null, CancellationToken.None)).Allowed, Is.False, "unknown source");
        }

        [Test]
        public async Task CallbackGuard_RequiringAJwt_RefusesMissingBadAndUnconfigured()
        {
            var noKeys = Guard(("Abdm:CallbackRequireJwt", "true"));
            Assert.That((await noKeys.CheckAsync(IPAddress.Loopback, null, CancellationToken.None)).Allowed, Is.False, "no bearer");
            Assert.That((await noKeys.CheckAsync(IPAddress.Loopback, "Bearer abc.def.ghi", CancellationToken.None)).Allowed, Is.False, "required but no JWKS url: fail closed");

            var badToken = Guard(("Abdm:CallbackRequireJwt", "true"), ("Abdm:CallbackJwksUrl", "http://127.0.0.1:1/jwks"));
            // the JWKS cannot even be fetched here, and a malformed token must never pass
            Assert.That((await badToken.CheckAsync(IPAddress.Loopback, "Bearer not-a-jwt", CancellationToken.None)).Allowed, Is.False);
            Assert.That((await badToken.CheckAsync(IPAddress.Loopback, "Basic abc", CancellationToken.None)).Allowed, Is.False);
        }
    }
}
