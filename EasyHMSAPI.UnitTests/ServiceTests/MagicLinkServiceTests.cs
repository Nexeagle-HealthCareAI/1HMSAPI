using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Services.Implementations;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Data.Enums;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.ServiceTests
{
    [TestFixture]
    public class MagicLinkServiceTests
    {
        private AppDbContext _context = null!;
        private Mock<IJwtAuthService> _jwt = null!;
        private List<Claim>? _issuedClaims;
        private User _user = null!;
        private Guid _hospitalId;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _issuedClaims = null;
            _jwt = new Mock<IJwtAuthService>();
            _jwt.Setup(j => j.GenerateJwtToken(It.IsAny<List<Claim>>()))
                .Callback<List<Claim>>(c => _issuedClaims = c)
                .Returns("signed-jwt");

            _user = TestDataFactory.SeedUser(_context, email: "dr@example.com", phone: "9000000001", role: "Doctor");
            var hospital = TestDataFactory.SeedHospital(_context, _user.UserID);
            _hospitalId = hospital.HospitalID;
            if (!_context.HospitalUsers.Any(h => h.UserID == _user.UserID && h.HospitalID == _hospitalId))
                _context.HospitalUsers.Add(new HospitalUser { HospitalUserID = Guid.NewGuid(), HospitalID = _hospitalId, UserID = _user.UserID });
            _context.SaveChanges();
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private MagicLinkService CreateService(Dictionary<string, string?>? settings = null)
        {
            var values = new Dictionary<string, string?> { ["WebApp:BaseUrl"] = "https://1hms-test.example.com/" };
            if (settings != null) foreach (var kv in settings) values[kv.Key] = kv.Value;
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
            return new MagicLinkService(_context, _jwt.Object, configuration, NullLogger<MagicLinkService>.Instance);
        }

        private static string TokenFrom(string url) => url[(url.IndexOf("#t=", StringComparison.Ordinal) + 3)..];

        [Test]
        public async Task CreateLink_PutsTokenInFragment_AndStoresOnlyItsHash()
        {
            var url = await CreateService().CreateLinkAsync(_user.UserID, _hospitalId, "/appointment-dashboard", "TEST");

            Assert.That(url, Does.StartWith("https://1hms-test.example.com/magic-login#t="));
            Assert.That(url, Does.Not.Contain("?"), "Token must not be in the query string (it would reach access logs).");

            var token = TokenFrom(url);
            var row = await _context.MagicLoginTokens.SingleAsync();
            Assert.That(token.Length, Is.GreaterThanOrEqualTo(43));
            Assert.That(row.TokenHash, Is.Not.EqualTo(token));
            Assert.That(row.TokenHash, Has.Length.EqualTo(64));
            Assert.That(row.UserId, Is.EqualTo(_user.UserID));
            Assert.That(row.HospitalId, Is.EqualTo(_hospitalId));
            Assert.That(row.TargetPath, Is.EqualTo("/appointment-dashboard"));
            Assert.That(row.ConsumedAt, Is.Null);
            Assert.That(row.ExpiresAt, Is.EqualTo(DateTime.UtcNow.AddHours(24)).Within(TimeSpan.FromMinutes(1)));
        }

        [Test]
        public async Task CreateLink_TwoLinksForTheSameUser_AreDifferent()
        {
            var service = CreateService();
            var a = await service.CreateLinkAsync(_user.UserID, _hospitalId, "/appointment-dashboard", "TEST");
            var b = await service.CreateLinkAsync(_user.UserID, _hospitalId, "/appointment-dashboard", "TEST");
            Assert.That(TokenFrom(a), Is.Not.EqualTo(TokenFrom(b)));
        }

        [TestCase("https://evil.example.com/x")]
        [TestCase("//evil.example.com")]
        [TestCase("appointment-dashboard")]
        [TestCase("/\\evil.example.com")]
        [TestCase("")]
        public void CreateLink_RejectsAnyTargetThatCouldLeaveTheApp(string target)
        {
            Assert.ThrowsAsync<ArgumentException>(() => CreateService().CreateLinkAsync(_user.UserID, _hospitalId, target, "TEST"));
        }

        [Test]
        public async Task CreateLink_LifetimeIsConfigurableButCapped()
        {
            await CreateService(new() { ["MagicLink:LifetimeHours"] = "1000" }).CreateLinkAsync(_user.UserID, _hospitalId, "/appointment-dashboard", "TEST");
            var row = await _context.MagicLoginTokens.SingleAsync();
            Assert.That(row.ExpiresAt, Is.EqualTo(DateTime.UtcNow.AddHours(72)).Within(TimeSpan.FromMinutes(1)));
        }

        [Test]
        public async Task CreateLink_PrunesLinksThatExpiredLongAgo()
        {
            _context.MagicLoginTokens.Add(new MagicLoginToken
            {
                TokenId = Guid.NewGuid(), TokenHash = new string('a', 64), UserId = _user.UserID, HospitalId = _hospitalId,
                TargetPath = "/x", Purpose = "OLD", CreatedAt = DateTime.UtcNow.AddDays(-40), ExpiresAt = DateTime.UtcNow.AddDays(-30),
            });
            await _context.SaveChangesAsync();

            await CreateService().CreateLinkAsync(_user.UserID, _hospitalId, "/appointment-dashboard", "TEST");

            var rows = await _context.MagicLoginTokens.ToListAsync();
            Assert.That(rows, Has.Count.EqualTo(1));
            Assert.That(rows[0].Purpose, Is.EqualTo("TEST"));
        }

        [Test]
        public async Task Exchange_ValidLink_IssuesSessionForThatUserAndHospital_AndBurnsTheToken()
        {
            var service = CreateService();
            var token = TokenFrom(await service.CreateLinkAsync(_user.UserID, _hospitalId, "/appointment-dashboard", "TEST"));

            var result = await service.ExchangeAsync(token, "203.0.113.9");

            Assert.That(result.Success, Is.True);
            Assert.That(result.AccessToken, Is.EqualTo("signed-jwt"));
            Assert.That(result.UserId, Is.EqualTo(_user.UserID));
            Assert.That(result.HospitalId, Is.EqualTo(_hospitalId));
            Assert.That(result.TargetPath, Is.EqualTo("/appointment-dashboard"));

            Assert.That(_issuedClaims!.Single(c => c.Type == "userId").Value, Is.EqualTo(_user.UserID.ToString()));
            Assert.That(_issuedClaims!.Single(c => c.Type == "roles").Value, Does.Contain("Doctor"));

            var row = await _context.MagicLoginTokens.SingleAsync();
            Assert.That(row.ConsumedAt, Is.Not.Null);
            Assert.That(row.ConsumedIp, Is.EqualTo("203.0.113.9"));

            var auth = await _context.UserAuths.SingleAsync(a => a.UserID == _user.UserID);
            Assert.That(auth.LoginMethod, Is.EqualTo("MagicLink"));
            Assert.That(auth.LastLoginTime, Is.Not.Null);
        }

        [Test]
        public async Task Exchange_SecondUseOfTheSameLink_IsRejected()
        {
            var service = CreateService();
            var token = TokenFrom(await service.CreateLinkAsync(_user.UserID, _hospitalId, "/appointment-dashboard", "TEST"));

            Assert.That((await service.ExchangeAsync(token, null)).Success, Is.True);
            var second = await service.ExchangeAsync(token, null);

            Assert.That(second.Success, Is.False);
            Assert.That(second.AccessToken, Is.Null);
            Assert.That(second.Message, Is.EqualTo(MagicLinkService.InvalidLinkMessage));
            _jwt.Verify(j => j.GenerateJwtToken(It.IsAny<List<Claim>>()), Times.Once);
        }

        [Test]
        public async Task Exchange_ExpiredLink_IsRejected()
        {
            var service = CreateService();
            var token = TokenFrom(await service.CreateLinkAsync(_user.UserID, _hospitalId, "/appointment-dashboard", "TEST"));
            var row = await _context.MagicLoginTokens.SingleAsync();
            row.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await _context.SaveChangesAsync();

            var result = await service.ExchangeAsync(token, null);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Is.EqualTo(MagicLinkService.InvalidLinkMessage));
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("short")]
        [TestCase("this-token-is-long-enough-but-was-never-issued-by-anyone")]
        public async Task Exchange_UnknownOrMalformedToken_IsRejectedWithTheSameGenericMessage(string token)
        {
            var result = await CreateService().ExchangeAsync(token, null);

            Assert.That(result.Success, Is.False);
            Assert.That(result.AccessToken, Is.Null);
            Assert.That(result.Message, Is.EqualTo(MagicLinkService.InvalidLinkMessage));
        }

        [Test]
        public async Task Exchange_UserDeactivatedAfterLinkWasSent_IsRejected_AndLinkIsNotBurned()
        {
            var service = CreateService();
            var token = TokenFrom(await service.CreateLinkAsync(_user.UserID, _hospitalId, "/appointment-dashboard", "TEST"));
            (await _context.Users.SingleAsync(u => u.UserID == _user.UserID)).UserStatusId = (int)UserStatusEnum.Inactive;
            await _context.SaveChangesAsync();

            var result = await service.ExchangeAsync(token, null);

            Assert.That(result.Success, Is.False);
            Assert.That((await _context.MagicLoginTokens.SingleAsync()).ConsumedAt, Is.Null);
        }

        [Test]
        public async Task Exchange_LockedAccount_IsRejected()
        {
            var service = CreateService();
            var token = TokenFrom(await service.CreateLinkAsync(_user.UserID, _hospitalId, "/appointment-dashboard", "TEST"));
            (await _context.UserAuths.SingleAsync(a => a.UserID == _user.UserID)).IsLocked = true;
            await _context.SaveChangesAsync();

            Assert.That((await service.ExchangeAsync(token, null)).Success, Is.False);
        }

        [Test]
        public async Task Exchange_UserRemovedFromTheHospital_IsRejected()
        {
            var service = CreateService();
            var token = TokenFrom(await service.CreateLinkAsync(_user.UserID, _hospitalId, "/appointment-dashboard", "TEST"));
            _context.HospitalUsers.RemoveRange(_context.HospitalUsers.Where(h => h.UserID == _user.UserID));
            await _context.SaveChangesAsync();

            var result = await service.ExchangeAsync(token, null);

            Assert.That(result.Success, Is.False);
            Assert.That(result.AccessToken, Is.Null);
        }
    }
}
