using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    // ABDM M2 phase 3: the readiness check tells a missing HIP ID (blocker) apart from a missing
    // callback URL (warning), and the activity list names silence after the window as no answer.
    [TestFixture]
    public class AbdmM2QueryTests
    {
        private AppDbContext _db = null!;
        private Guid _hospitalId;

        [SetUp]
        public void SetUp()
        {
            _db = InMemoryDbContextFactory.CreateContext();
            _hospitalId = Guid.NewGuid();
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_db);
            _db.Dispose();
        }

        private static IConfiguration Config(bool bridge) => new ConfigurationBuilder()
            .AddInMemoryCollection(bridge
                ? new Dictionary<string, string?> { ["Abdm:PublicBaseUrl"] = "https://api.example.test", ["Abdm:CallbackSecret"] = "s3cret" }
                : new Dictionary<string, string?> { ["Abdm:CallbackSecret"] = "<set-via-env>" })
            .Build();

        private Task<Application.ResponseModels.QueryResponseModels.AbdmM2ReadinessResponseModel> Readiness(bool bridge) =>
            new GetAbdmM2ReadinessHandler(_db, Config(bridge)).Handle(new GetAbdmM2ReadinessRequestModel { HospitalId = _hospitalId }, CancellationToken.None);

        private void SaveHip()
        {
            _db.AbdmFacilities.Add(new AbdmFacility { HospitalId = _hospitalId, HipId = "IN2810014366", UpdatedAt = DateTime.UtcNow });
            _db.SaveChanges();
        }

        [Test]
        public async Task MissingHipId_IsABlocker()
        {
            var r = await Readiness(bridge: true);
            Assert.That(r.Issues.Count(i => i.Level == "blocker"), Is.EqualTo(1));
            Assert.That(r.Issues.First().Level, Is.EqualTo("blocker"), "the blocker is listed first");
            Assert.That(r.HipId, Is.Null);
        }

        [Test]
        public async Task MissingBridgeSettings_IsAWarning_PlaceholderCountsAsMissing()
        {
            SaveHip();
            var r = await Readiness(bridge: false);
            Assert.That(r.BridgeSettingsPresent, Is.False);
            Assert.That(r.Issues.Single().Level, Is.EqualTo("warning"));
        }

        [Test]
        public async Task BridgeSetButNothingEverArrived_WarnsToRegister()
        {
            SaveHip();
            var r = await Readiness(bridge: true);
            Assert.That(r.Issues.Single().Text, Does.Contain("Register bridge"));
        }

        [Test]
        public async Task AllInPlace_NoIssues()
        {
            SaveHip();
            _db.AbdmInbounds.Add(new AbdmInbound { InboundId = Guid.NewGuid(), HospitalId = _hospitalId, Path = "p", HeaderNames = "", Verdict = AbdmInboundVerdict.Accepted, ReceivedAt = DateTime.UtcNow });
            _db.SaveChanges();

            var r = await Readiness(bridge: true);

            Assert.That(r.Issues, Is.Empty);
            Assert.That(r.LastCallbackAt, Is.Not.Null);
        }

        [Test]
        public async Task Exchanges_SilenceAfterTheWindowIsNoAnswer_AnsweredShowsTheWait_OtherHospitalsHidden()
        {
            var sent = DateTime.UtcNow.AddMinutes(-5);
            var inboundId = Guid.NewGuid();
            _db.AbdmInbounds.Add(new AbdmInbound { InboundId = inboundId, HospitalId = _hospitalId, Path = "api/v3/link/on_carecontext", HeaderNames = "Authorization:20", Verdict = AbdmInboundVerdict.Accepted, ReceivedAt = sent.AddMilliseconds(340) });
            _db.AbdmExchanges.AddRange(
                new AbdmExchange { ExchangeId = Guid.NewGuid(), RequestId = "silent", HospitalId = _hospitalId, Kind = "link-token", Path = "p", ExpectsCallback = true, Status = AbdmExchangeStatus.Accepted, SentAt = sent, AcceptedAt = sent },
                new AbdmExchange { ExchangeId = Guid.NewGuid(), RequestId = "answered", HospitalId = _hospitalId, Kind = "link-carecontext", Path = "p", ExpectsCallback = true, Status = AbdmExchangeStatus.Answered, SentAt = sent, AcceptedAt = sent, AnsweredAt = sent.AddMilliseconds(340), AnsweredByInboundId = inboundId },
                new AbdmExchange { ExchangeId = Guid.NewGuid(), RequestId = "elsewhere", HospitalId = Guid.NewGuid(), Kind = "link-token", Path = "p", Status = AbdmExchangeStatus.Accepted, SentAt = sent });
            _db.SaveChanges();

            var r = await new GetAbdmExchangesHandler(_db, Config(true)).Handle(new GetAbdmExchangesRequestModel { HospitalId = _hospitalId }, CancellationToken.None);

            Assert.That(r.Exchanges.Select(e => e.RequestId), Is.EquivalentTo(new[] { "silent", "answered" }));
            Assert.That(r.Exchanges.Single(e => e.RequestId == "silent").Status, Is.EqualTo("NO_ANSWER"));
            var answered = r.Exchanges.Single(e => e.RequestId == "answered");
            Assert.That(answered.WaitMs, Is.EqualTo(340));
            Assert.That(answered.CallbackPath, Is.EqualTo("api/v3/link/on_carecontext"));
        }

        [Test]
        public async Task InboundRequests_UnprocessedIsUnanswered_AnswersAreExcluded()
        {
            _db.AbdmInbounds.AddRange(
                new AbdmInbound { InboundId = Guid.NewGuid(), HospitalId = _hospitalId, Path = "api/v3/hip/patient/care-context/discover", HeaderNames = "", Verdict = AbdmInboundVerdict.Accepted, ReceivedAt = DateTime.UtcNow },
                new AbdmInbound { InboundId = Guid.NewGuid(), HospitalId = _hospitalId, Path = "api/v3/link/on_carecontext", AnswersRequestId = "x", HeaderNames = "", Verdict = AbdmInboundVerdict.Accepted, ReceivedAt = DateTime.UtcNow });
            _db.SaveChanges();

            var r = await new GetAbdmInboundRequestsHandler(_db).Handle(new GetAbdmInboundRequestsRequestModel { HospitalId = _hospitalId }, CancellationToken.None);

            Assert.That(r.Requests.Single().Unanswered, Is.True);
        }
    }
}
