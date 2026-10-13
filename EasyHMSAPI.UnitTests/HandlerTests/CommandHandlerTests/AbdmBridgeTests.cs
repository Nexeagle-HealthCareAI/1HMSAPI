using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Application.Services.Implementations;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    // ABDM M2 phase 1: every outbound call is an exchange saved before it is sent, and every
    // callback is recorded and checked (bearer present; an answer answers a call we sent; otherwise
    // the facility header names one of ours) before anything acts on it.
    [TestFixture]
    public class AbdmBridgeTests
    {
        private AppDbContext _db = null!;
        private Guid _hospitalId;
        private const string HipId = "IN2810014366";

        [SetUp]
        public void SetUp()
        {
            _db = InMemoryDbContextFactory.CreateContext();
            _hospitalId = Guid.NewGuid();
            _db.AbdmFacilities.Add(new AbdmFacility { HospitalId = _hospitalId, HipId = HipId, UpdatedAt = DateTime.UtcNow });
            _db.SaveChanges();
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_db);
            _db.Dispose();
        }

        private static List<KeyValuePair<string, string>> Headers(bool bearer = true, string? hip = null, string? requestId = "cb-1")
        {
            var list = new List<KeyValuePair<string, string>>();
            if (bearer) list.Add(new("Authorization", "Bearer eyJ.secret.token"));
            if (hip != null) list.Add(new("X-HIP-ID", hip));
            if (requestId != null) list.Add(new("REQUEST-ID", requestId));
            return list;
        }

        private static string Answer(string requestId, string extra = "") =>
            $"{{\"abhaAddress\":\"a@sbx\"{extra},\"response\":{{\"requestId\":\"{requestId}\"}}}}";

        private AbdmExchange SeedExchange(string requestId, string status = AbdmExchangeStatus.Accepted)
        {
            var exchange = new AbdmExchange
            {
                ExchangeId = Guid.NewGuid(), RequestId = requestId, HospitalId = _hospitalId, FacilityId = HipId,
                Kind = "link-token", Path = "/api/hiecm/v3/token/generate-token", ExpectsCallback = true,
                Status = status, SentAt = DateTime.UtcNow
            };
            _db.AbdmExchanges.Add(exchange);
            _db.SaveChanges();
            return exchange;
        }

        // ---------------------------------------------------------------- inspector

        [Test]
        public void HeaderSummary_HasNamesAndLengths_NeverValues()
        {
            var summary = AbdmCallbackInspector.DescribeHeaders(Headers(hip: HipId));
            Assert.That(summary, Does.Contain($"Authorization:{"Bearer eyJ.secret.token".Length}"));
            Assert.That(summary, Does.Contain($"X-HIP-ID:{HipId.Length}"));
            Assert.That(summary, Does.Not.Contain("secret"));
            Assert.That(summary, Does.Not.Contain(HipId));
        }

        [TestCase("{\"response\":{\"requestId\":\"r-1\"}}", "r-1")]
        [TestCase("{\"resp\":{\"requestId\":\"r-2\"}}", "r-2")]
        [TestCase("{\"transactionId\":\"t\"}", null)]
        [TestCase("not json", null)]
        [TestCase("", null)]
        public void AnswersRequestId_ReadsTheEnvelope(string body, string? expected)
        {
            Assert.That(AbdmCallbackInspector.AnswersRequestId(body), Is.EqualTo(expected));
        }

        [Test]
        public void Bearer_MustBePresentAndNonEmpty()
        {
            Assert.That(AbdmCallbackInspector.HasBearer(Headers()), Is.True);
            Assert.That(AbdmCallbackInspector.HasBearer(Headers(bearer: false)), Is.False);
            Assert.That(AbdmCallbackInspector.HasBearer(new[] { new KeyValuePair<string, string>("Authorization", "Bearer ") }), Is.False);
            Assert.That(AbdmCallbackInspector.HasBearer(new[] { new KeyValuePair<string, string>("Authorization", "Basic abc") }), Is.False);
        }

        // ---------------------------------------------------------------- inbound

        [Test]
        public async Task ValidAnswerWithoutAuthorization_IsRejected_BodyNotStored_ExchangeUntouched()
        {
            var exchange = SeedExchange("req-1");

            var inbound = await new AbdmExchangeLog(_db).RecordInboundAsync(
                new AbdmInboundDelivery("api/v3/hip/token/on-generate-token", Headers(bearer: false), Answer("req-1")), CancellationToken.None);

            Assert.That(inbound.Verdict, Is.EqualTo(AbdmInboundVerdict.NoBearer));
            Assert.That(inbound.RawBody, Is.Null, "a rejected delivery is logged, never stored");
            Assert.That(_db.AbdmInbounds.Count(), Is.EqualTo(1), "the rejection itself is logged");
            Assert.That(_db.AbdmExchanges.Single(e => e.ExchangeId == exchange.ExchangeId).Status, Is.EqualTo(AbdmExchangeStatus.Accepted));
        }

        [Test]
        public async Task AnswerToACallWeSent_ClosesTheExchange()
        {
            var exchange = SeedExchange("req-2");

            var inbound = await new AbdmExchangeLog(_db).RecordInboundAsync(
                new AbdmInboundDelivery("api/v3/hip/token/on-generate-token", Headers(), Answer("req-2", ",\"linkToken\":\"x\"")), CancellationToken.None);

            var stored = _db.AbdmExchanges.Single(e => e.ExchangeId == exchange.ExchangeId);
            Assert.That(inbound.Verdict, Is.EqualTo(AbdmInboundVerdict.Accepted));
            Assert.That(inbound.RawBody, Does.Contain("linkToken"));
            Assert.That(inbound.HospitalId, Is.EqualTo(_hospitalId), "hospital comes from the exchange it answers");
            Assert.That(stored.Status, Is.EqualTo(AbdmExchangeStatus.Answered));
            Assert.That(stored.AnsweredByInboundId, Is.EqualTo(inbound.InboundId));
        }

        [Test]
        public async Task AnswerToACallWeNeverSent_IsRejected()
        {
            var inbound = await new AbdmExchangeLog(_db).RecordInboundAsync(
                new AbdmInboundDelivery("api/v3/link/on_carecontext", Headers(), Answer("never-sent")), CancellationToken.None);

            Assert.That(inbound.Verdict, Is.EqualTo(AbdmInboundVerdict.UnknownRequest));
            Assert.That(inbound.RawBody, Is.Null);
        }

        [Test]
        public async Task SecondDeliveryOfTheSameAnswer_IsADuplicate()
        {
            SeedExchange("req-3");
            var log = new AbdmExchangeLog(_db);
            await log.RecordInboundAsync(new AbdmInboundDelivery("p", Headers(), Answer("req-3")), CancellationToken.None);

            var second = await log.RecordInboundAsync(new AbdmInboundDelivery("p", Headers(requestId: "cb-2"), Answer("req-3")), CancellationToken.None);

            Assert.That(second.Verdict, Is.EqualTo(AbdmInboundVerdict.Duplicate));
        }

        [Test]
        public async Task CallbackCarryingAnError_RecordsTheCodeOnTheExchange()
        {
            var exchange = SeedExchange("req-4");

            await new AbdmExchangeLog(_db).RecordInboundAsync(new AbdmInboundDelivery("p", Headers(),
                "{\"error\":{\"code\":\"ABDM-1092: \",\"message\":\"Duplicate link token request\"},\"response\":{\"requestId\":\"req-4\"}}"),
                CancellationToken.None);

            var stored = _db.AbdmExchanges.Single(e => e.ExchangeId == exchange.ExchangeId);
            Assert.That(stored.Status, Is.EqualTo(AbdmExchangeStatus.Answered));
            Assert.That(stored.ErrorCode, Is.EqualTo("ABDM-1092"));
            Assert.That(stored.ErrorMessage, Is.EqualTo("Duplicate link token request"));
        }

        [Test]
        public async Task UnsolicitedDelivery_ForAFacilityWeDoNotServe_IsRejected()
        {
            var inbound = await new AbdmExchangeLog(_db).RecordInboundAsync(new AbdmInboundDelivery(
                "api/v3/consent/request/hip/notify", Headers(hip: "SOMEONE_ELSE"), "{\"notification\":{}}"), CancellationToken.None);

            Assert.That(inbound.Verdict, Is.EqualTo(AbdmInboundVerdict.UnknownFacility));
            Assert.That(inbound.RawBody, Is.Null, "another organisation's patient data is never stored");
        }

        [Test]
        public async Task UnsolicitedDelivery_WithNoFacilityHeader_IsRejected()
        {
            var inbound = await new AbdmExchangeLog(_db).RecordInboundAsync(new AbdmInboundDelivery(
                "api/v3/hip/patient/care-context/discover", Headers(), "{\"transactionId\":\"t\"}"), CancellationToken.None);

            Assert.That(inbound.Verdict, Is.EqualTo(AbdmInboundVerdict.UnknownFacility));
        }

        [Test]
        public async Task UnsolicitedDelivery_ForOurFacility_IsAccepted_CaseInsensitively()
        {
            var inbound = await new AbdmExchangeLog(_db).RecordInboundAsync(new AbdmInboundDelivery(
                "api/v3/hip/patient/care-context/discover", Headers(hip: HipId.ToLowerInvariant()), "{\"transactionId\":\"t\"}"), CancellationToken.None);

            Assert.That(inbound.Verdict, Is.EqualTo(AbdmInboundVerdict.Accepted));
            Assert.That(inbound.HospitalId, Is.EqualTo(_hospitalId));
            Assert.That(inbound.RawBody, Does.Contain("transactionId"));
        }

        // ---------------------------------------------------------------- outbound

        private sealed class StubHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _respond;
            public List<HttpRequestMessage> Requests { get; } = new();
            public StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) => _respond = respond;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                return _respond(request);
            }
        }

        private static HttpResponseMessage Respond(HttpStatusCode status, string body = "") =>
            new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

        private static AbdmHieService Hie(AppDbContext db, HttpMessageHandler handler)
        {
            var gateway = new Mock<IAbdmGatewayService>();
            gateway.Setup(g => g.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("gateway-token");
            var env = new Mock<IHostEnvironment>();
            env.SetupGet(e => e.EnvironmentName).Returns("Development");
            var config = new ConfigurationBuilder().Build();
            return new AbdmHieService(new HttpClient(handler), gateway.Object, new AbdmExchangeLog(db), db, config, env.Object, NullLogger<AbdmHieService>.Instance);
        }

        private AbdmHieCall LinkTokenCall(Guid? hospitalId = null) => new()
        {
            Kind = "link-token",
            Path = "/api/hiecm/v3/token/generate-token",
            Body = new { abhaAddress = "a@sbx" },
            HospitalId = hospitalId ?? _hospitalId,
            SubjectRef = "a@sbx",
            ExpectsCallback = true,
            LinkToken = "link-jwt"
        };

        [Test]
        public void NoHipId_IsRefusedLocally_NothingSent_AndTheSettingIsNamed()
        {
            var handler = new StubHandler(_ => Task.FromResult(Respond(HttpStatusCode.Accepted)));

            var ex = Assert.ThrowsAsync<AbdmException>(() => Hie(_db, handler).SendAsync(LinkTokenCall(Guid.NewGuid()), CancellationToken.None));

            Assert.That(ex!.Code, Is.EqualTo(AbdmHieService.MissingHipIdCode));
            Assert.That(ex.Message, Does.Contain("Facility (HIP / HFR) ID"));
            Assert.That(handler.Requests, Is.Empty);
            Assert.That(_db.AbdmExchanges.Single().Status, Is.EqualTo(AbdmExchangeStatus.Refused));
        }

        [Test]
        public async Task Accepted_IsWaiting_AndTheSavedRequestIdIsTheOneSent()
        {
            var handler = new StubHandler(_ => Task.FromResult(Respond(HttpStatusCode.Accepted)));

            var result = await Hie(_db, handler).SendAsync(LinkTokenCall(), CancellationToken.None);

            var sent = handler.Requests.Single();
            var stored = _db.AbdmExchanges.Single();
            Assert.That(stored.Status, Is.EqualTo(AbdmExchangeStatus.Accepted));
            Assert.That(stored.HttpStatus, Is.EqualTo(202));
            Assert.That(sent.Headers.GetValues("REQUEST-ID").Single(), Is.EqualTo(stored.RequestId));
            Assert.That(result.RequestId, Is.EqualTo(stored.RequestId));
            Assert.That(sent.Headers.GetValues("X-HIP-ID").Single(), Is.EqualTo(HipId));
            Assert.That(sent.Headers.GetValues("X-LINK-TOKEN").Single(), Is.EqualTo("link-jwt"));
            Assert.That(sent.RequestUri!.ToString(), Is.EqualTo("https://dev.abdm.gov.in/api/hiecm/v3/token/generate-token"));
        }

        [Test]
        public void SynchronousRefusal_IsRecordedAsRefused_NotWaiting()
        {
            var handler = new StubHandler(_ => Task.FromResult(Respond(HttpStatusCode.BadRequest,
                "{\"error\":{\"code\":\"ABDM-9999\",\"message\":\"User not found\"}}")));

            var ex = Assert.ThrowsAsync<AbdmException>(() => Hie(_db, handler).SendAsync(LinkTokenCall(), CancellationToken.None));

            var stored = _db.AbdmExchanges.Single();
            Assert.That(ex!.HasCode("ABDM-9999"), Is.True);
            Assert.That(stored.Status, Is.EqualTo(AbdmExchangeStatus.Refused));
            Assert.That(stored.ErrorCode, Is.EqualTo("ABDM-9999"));
            Assert.That(stored.HttpStatus, Is.EqualTo(400));
        }

        [Test]
        public async Task CallbackThatBeatsTheHttpResponse_StaysAnswered()
        {
            // Two contexts on one database: the callback is saved by another request scope while
            // the outbound call is still waiting for its 202.
            var services = new ServiceCollection().AddEntityFrameworkInMemoryDatabase().BuildServiceProvider();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"Race_{Guid.NewGuid()}")
                .UseInternalServiceProvider(services)
                .Options;
            using var outboundDb = new AppDbContext(options);
            using var callbackDb = new AppDbContext(options);
            outboundDb.AbdmFacilities.Add(new AbdmFacility { HospitalId = _hospitalId, HipId = HipId, UpdatedAt = DateTime.UtcNow });
            outboundDb.SaveChanges();

            var handler = new StubHandler(async request =>
            {
                var requestId = request.Headers.GetValues("REQUEST-ID").Single();
                await new AbdmExchangeLog(callbackDb).RecordInboundAsync(
                    new AbdmInboundDelivery("api/v3/hip/token/on-generate-token", Headers(), Answer(requestId)), CancellationToken.None);
                return Respond(HttpStatusCode.Accepted);
            });

            await Hie(outboundDb, handler).SendAsync(LinkTokenCall(), CancellationToken.None);

            var stored = new AppDbContext(options).AbdmExchanges.Single();
            Assert.That(stored.Status, Is.EqualTo(AbdmExchangeStatus.Answered));
            Assert.That(stored.HttpStatus, Is.EqualTo(202));
        }
    }
}
