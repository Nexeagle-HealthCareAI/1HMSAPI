#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Application.Services.Fhir;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    // ABDM M2 phase 7: consent and data flow as the HIP. Nothing is sent without a consent that is
    // granted, in date and names the care context; what is sent can be read only by the HIU whose
    // key the request carried; and ABDM is always told how the transfer ended.
    [TestFixture]
    public class AbdmDataFlowTests
    {
        // The HIU's side of the exchange (the same known keys as AbdmFideliusTests).
        private const string HiuPrivate = "D2aWzZJ9miOoVnZe0mLWpkrSzNdAUFzRDXtJ1pZr1Rs=";
        private const string HiuPublic = "BAy/BgpqAp4wA6W5NasOKyBnZPKAihkRjXjd6n5fKvzmekJcA12CsuGfsg+Fc1WxNPgUlI1qztS9q7T88RGbHPY=";
        private const string HiuNonce = "PKeK26YEzZ1O1nKqeCMpVtDmvNT/bbzLr5AZexZ6CiE=";
        private const string PushUrl = "https://hiu.example.org/data/push";
        private const string Reference = "OPD-visit-1";
        private const string RecordJson = "{\"resourceType\":\"Bundle\",\"id\":\"the-record\"}";

        private AppDbContext _db = null!;
        private Mock<IAbdmHieService> _hie = null!;
        private Mock<IAbdmRecordSource> _records = null!;
        private Mock<IAbdmDataPushClient> _push = null!;
        private List<AbdmHieCall> _calls = null!;
        private string? _pushedUrl, _pushedJson;
        private int _pushStatus;
        private Guid _hospitalId;
        private DateTime _visitDate;

        [SetUp]
        public void SetUp()
        {
            _db = InMemoryDbContextFactory.CreateContext();
            _hospitalId = Guid.NewGuid();
            _visitDate = new DateTime(2026, 10, 3, 5, 0, 0, DateTimeKind.Utc);
            _db.AbdmFacilities.Add(new AbdmFacility { HospitalId = _hospitalId, HipId = "IN2810014366", UpdatedAt = DateTime.UtcNow });
            _db.AbdmCareContexts.Add(new AbdmCareContext
            {
                CareContextId = Guid.NewGuid(), HospitalId = _hospitalId, PatientId = "P-0001", SourceType = "OPD", SourceId = Guid.NewGuid(),
                ReferenceNumber = Reference, Display = "OPD visit on 03 Oct 2026", HiTypes = "Prescription", LinkStatus = AbdmLinkStatus.Linked,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });
            _db.SaveChanges();

            _calls = new List<AbdmHieCall>();
            _hie = new Mock<IAbdmHieService>();
            _hie.Setup(h => h.SendAsync(It.IsAny<AbdmHieCall>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((AbdmHieCall call, CancellationToken _) =>
                {
                    _calls.Add(call);
                    return new AbdmHieResult { ExchangeId = Guid.NewGuid(), RequestId = Guid.NewGuid().ToString(), HttpStatus = 202 };
                });

            _records = new Mock<IAbdmRecordSource>();
            _records.Setup(r => r.BuildAsync(It.IsAny<AbdmCareContext>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new AbdmBuiltRecords { WhenUtc = _visitDate, Records = new[] { new AbdmBuiltRecord("Prescription", RecordJson) } });

            _pushStatus = 202;
            _pushedUrl = null;
            _pushedJson = null;
            _push = new Mock<IAbdmDataPushClient>();
            _push.Setup(p => p.PushAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string url, string json, CancellationToken _) => { _pushedUrl = url; _pushedJson = json; return _pushStatus; });
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_db);
            _db.Dispose();
        }

        private AbdmDataFlowService Service() => new(_db, _hie.Object, _records.Object, _push.Object, NullLogger<AbdmDataFlowService>.Instance);

        private AbdmInbound Inbound(string requestId, string body) => new()
        {
            InboundId = Guid.NewGuid(), HospitalId = _hospitalId, HeaderRequestId = requestId, RawBody = body,
            Verdict = AbdmInboundVerdict.Accepted, ReceivedAt = DateTime.UtcNow
        };

        private static string ConsentBody(string status = "GRANTED", string reference = Reference, string? eraseAt = null) => JsonSerializer.Serialize(new
        {
            notification = new
            {
                status,
                consentId = "consent-1",
                consentDetail = new
                {
                    consentId = "consent-1",
                    patient = new { id = "asha@sbx" },
                    careContexts = new[] { new { patientReference = "P-0001", careContextReference = reference } },
                    purpose = new { text = "Care Management", code = "CAREMGT" },
                    hip = new { id = "IN2810014366", name = "City Hospital" },
                    hiu = new { id = "hiu-9", name = "Other Clinic" },
                    hiTypes = new[] { "Prescription" },
                    permission = new
                    {
                        accessMode = "VIEW",
                        dateRange = new { from = "2026-01-01T00:00:00.000Z", to = "2026-12-31T00:00:00.000Z" },
                        dataEraseAt = eraseAt ?? "2099-01-01T00:00:00.000Z"
                    }
                }
            }
        });

        private static string RequestBody(string url = PushUrl, string from = "2026-01-01T00:00:00.000Z", string to = "2026-12-31T00:00:00.000Z") => JsonSerializer.Serialize(new
        {
            transactionId = "txn-9",
            hiRequest = new
            {
                consent = new { id = "consent-1" },
                dateRange = new { from, to },
                dataPushUrl = url,
                keyMaterial = new
                {
                    cryptoAlg = "ECDH",
                    curve = "Curve25519",
                    dhPublicKey = new { expiry = "2099-01-01T00:00:00.000Z", parameters = "Curve25519/32byte random key", keyValue = HiuPublic },
                    nonce = HiuNonce
                }
            }
        });

        private static JsonElement Body(AbdmHieCall call) => JsonDocument.Parse(JsonSerializer.Serialize(call.Body)).RootElement;

        private async Task GrantAsync(string body) => await Service().OnConsentNotifyAsync(Inbound("req-consent", body), CancellationToken.None);

        // ---------------------------------------------------------------- rules

        [Test]
        public void APushUrl_MustBeHttps_AndMustNotPointIntoOurNetwork()
        {
            Assert.That(AbdmDataFlowRules.IsSafePushUrl(PushUrl), Is.True);
            foreach (var url in new[] { "http://hiu.example.org/push", "https://localhost/push", "https://127.0.0.1/push", "https://10.0.0.5/push", "https://192.168.1.9/push", "https://169.254.169.254/latest", "not a url", null })
                Assert.That(AbdmDataFlowRules.IsSafePushUrl(url), Is.False, url);
        }

        [Test]
        public void OnlyPublicAddresses_AreConnectedTo()
        {
            foreach (var ip in new[] { "8.8.8.8", "151.185.45.77", "2606:4700:4700::1111" })
                Assert.That(AbdmDataFlowRules.IsPublicAddress(System.Net.IPAddress.Parse(ip)), Is.True, ip);
            foreach (var ip in new[] { "127.0.0.1", "10.1.2.3", "172.20.0.1", "192.168.0.1", "169.254.169.254", "100.64.0.1", "0.0.0.0", "224.0.0.1",
                                       "::1", "fe80::1", "fd00::1", "::ffff:10.0.0.1", "::ffff:127.0.0.1" })
                Assert.That(AbdmDataFlowRules.IsPublicAddress(System.Net.IPAddress.Parse(ip)), Is.False, ip);
            Assert.That(AbdmDataFlowRules.IsSafePushUrl("https://[::ffff:10.0.0.1]/push"), Is.False);
            Assert.That(AbdmDataFlowRules.IsSafePushUrl("https://100.64.1.1/push"), Is.False);
        }

        [Test]
        public void ThePushClient_DoesNotFollowRedirects()
        {
            using var handler = AbdmDataPushClient.CreateHandler();
            Assert.That(handler.AllowAutoRedirect, Is.False);
            Assert.That(handler.ConnectCallback, Is.Not.Null, "the resolved address is checked when connecting");
        }

        [TestCase(null, true)]
        [TestCase("false", false)]
        [TestCase("true", true)]
        public async Task OurGatewayToken_GoesToTheHiu_UnlessSwitchedOff(string? setting, bool sent)
        {
            var seen = new List<System.Net.Http.HttpRequestMessage>();
            var http = new System.Net.Http.HttpClient(new RecordingHandler(seen));
            var gateway = new Mock<IAbdmGatewayService>();
            gateway.Setup(g => g.GetAccessTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync("gateway-token");
            var configuration = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
            configuration.Setup(c => c[AbdmDataPushClient.SendGatewayTokenKey]).Returns(setting);

            var status = await new AbdmDataPushClient(http, gateway.Object, configuration.Object).PushAsync(PushUrl, "{}", CancellationToken.None);

            Assert.That(status, Is.EqualTo(202));
            Assert.That(seen.Single().Headers.Authorization?.Parameter, sent ? Is.EqualTo("gateway-token") : Is.Null);
        }

        private sealed class RecordingHandler : System.Net.Http.HttpMessageHandler
        {
            private readonly List<System.Net.Http.HttpRequestMessage> _seen;
            public RecordingHandler(List<System.Net.Http.HttpRequestMessage> seen) => _seen = seen;
            protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request, CancellationToken cancellationToken)
            {
                _seen.Add(request);
                return Task.FromResult(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.Accepted));
            }
        }

        [Test]
        public void InRange_UsesTheNarrowerOfTheConsentAndTheRequest()
        {
            var consent = new AbdmConsent { DateFrom = new DateTime(2026, 1, 1), DateTo = new DateTime(2026, 12, 31) };
            var march = new AbdmHiRequest { From = new DateTime(2026, 3, 1), To = new DateTime(2026, 3, 31) };

            Assert.That(AbdmDataFlowRules.InRange(new DateTime(2026, 3, 15), consent, march), Is.True);
            Assert.That(AbdmDataFlowRules.InRange(new DateTime(2026, 5, 15), consent, march), Is.False);
            Assert.That(AbdmDataFlowRules.InRange(new DateTime(2025, 12, 15), consent, new AbdmHiRequest()), Is.False);
            Assert.That(AbdmDataFlowRules.InRange(new DateTime(2020, 1, 1), new AbdmConsent(), new AbdmHiRequest()), Is.True);
        }

        // ---------------------------------------------------------------- consent

        [Test]
        public async Task ConsentGranted_IsStored_AndAcknowledged()
        {
            await GrantAsync(ConsentBody());

            var consent = _db.AbdmConsents.Single();
            Assert.That(consent.Role, Is.EqualTo(AbdmConsentRole.Hip));
            Assert.That(consent.Status, Is.EqualTo(AbdmConsentStatus.Granted));
            Assert.That(consent.PatientAbhaAddress, Is.EqualTo("asha@sbx"));
            Assert.That(consent.CounterpartyId, Is.EqualTo("hiu-9"));
            Assert.That(consent.HiTypes, Is.EqualTo("Prescription"));
            Assert.That(consent.CareContextReferences, Is.EqualTo(Reference));
            Assert.That(consent.DateFrom, Is.EqualTo(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));

            var body = Body(_calls.Single());
            Assert.That(_calls.Single().Path, Does.EndWith("consent/v3/request/hip/on-notify"));
            Assert.That(body.GetProperty("acknowledgement").GetProperty("status").GetString(), Is.EqualTo("OK"));
            Assert.That(body.GetProperty("acknowledgement").GetProperty("consentId").GetString(), Is.EqualTo("consent-1"));
            Assert.That(body.GetProperty("response").GetProperty("requestId").GetString(), Is.EqualTo("req-consent"));
        }

        [Test]
        public async Task ConsentRevoked_KeepsTheDetail_AndChangesTheStatus()
        {
            await GrantAsync(ConsentBody());

            await GrantAsync(JsonSerializer.Serialize(new { notification = new { status = "REVOKED", consentId = "consent-1" } }));

            var consent = _db.AbdmConsents.Single();
            Assert.That(consent.Status, Is.EqualTo(AbdmConsentStatus.Revoked));
            Assert.That(consent.CareContextReferences, Is.EqualTo(Reference));
        }

        // ---------------------------------------------------------------- data request

        [Test]
        public async Task ARequestUnderAGrantedConsent_SendsRecordsOnlyTheHiuCanRead_AndReportsIt()
        {
            await GrantAsync(ConsentBody());
            _calls.Clear();

            await Service().OnHealthInformationRequestAsync(Inbound("req-data", RequestBody()), CancellationToken.None);

            var ack = Body(_calls[0]);
            Assert.That(_calls[0].Path, Does.EndWith("health-information/hip/on-request"));
            Assert.That(ack.GetProperty("hiRequest").GetProperty("sessionStatus").GetString(), Is.EqualTo("ACKNOWLEDGED"));
            Assert.That(ack.GetProperty("response").GetProperty("requestId").GetString(), Is.EqualTo("req-data"));

            Assert.That(_pushedUrl, Is.EqualTo(PushUrl));
            var pushed = JsonDocument.Parse(_pushedJson!).RootElement;
            Assert.That(pushed.GetProperty("transactionId").GetString(), Is.EqualTo("txn-9"));
            Assert.That(_pushedJson, Does.Not.Contain("the-record"), "the record never travels in the clear");
            var entry = pushed.GetProperty("entries")[0];
            Assert.That(entry.GetProperty("careContextReference").GetString(), Is.EqualTo(Reference));
            Assert.That(entry.GetProperty("media").GetString(), Is.EqualTo("application/fhir+json"));
            Assert.That(entry.GetProperty("checksum").GetString(), Is.EqualTo(AbdmDataFlowRules.Md5Hex(RecordJson)));
            var keys = pushed.GetProperty("keyMaterial");
            var decrypted = AbdmFidelius.Decrypt(entry.GetProperty("content").GetString()!, HiuPrivate, HiuNonce,
                keys.GetProperty("dhPublicKey").GetProperty("keyValue").GetString()!, keys.GetProperty("nonce").GetString()!);
            Assert.That(decrypted, Is.EqualTo(RecordJson));

            var notify = Body(_calls[1]).GetProperty("notification");
            Assert.That(_calls[1].Path, Does.EndWith("health-information/notify"));
            Assert.That(notify.GetProperty("statusNotification").GetProperty("sessionStatus").GetString(), Is.EqualTo("TRANSFERRED"));
            Assert.That(notify.GetProperty("statusNotification").GetProperty("statusResponses")[0].GetProperty("hiStatus").GetString(), Is.EqualTo("DELIVERED"));
            Assert.That(notify.GetProperty("notifier").GetProperty("id").GetString(), Is.EqualTo("IN2810014366"));

            var transfer = _db.AbdmDataTransfers.Single();
            Assert.That(transfer.Status, Is.EqualTo(AbdmTransferStatus.Transferred));
            Assert.That(transfer.RecordCount, Is.EqualTo(1));
        }

        [TestCase("REVOKED", null, AbdmDataFlowRules.ConsentNotGrantedCode)]
        [TestCase("GRANTED", "2020-01-01T00:00:00.000Z", AbdmDataFlowRules.ConsentExpiredCode)]
        public async Task ARequestUnderARevokedOrExpiredConsent_IsRefused_AndNothingIsSent(string status, string? eraseAt, string expectedCode)
        {
            await GrantAsync(ConsentBody(status, Reference, eraseAt));
            _calls.Clear();

            await Service().OnHealthInformationRequestAsync(Inbound("req-data", RequestBody()), CancellationToken.None);

            Assert.That(Body(_calls.Single()).GetProperty("error").GetProperty("code").GetString(), Is.EqualTo(expectedCode));
            _push.Verify(p => p.PushAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            _records.Verify(r => r.BuildAsync(It.IsAny<AbdmCareContext>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()), Times.Never);
            Assert.That(_db.AbdmDataTransfers.Single().Status, Is.EqualTo(AbdmTransferStatus.Refused));
        }

        [Test]
        public async Task ARequestWithNoConsentOnRecord_IsRefused()
        {
            await Service().OnHealthInformationRequestAsync(Inbound("req-data", RequestBody()), CancellationToken.None);

            Assert.That(Body(_calls.Single()).GetProperty("error").GetProperty("code").GetString(), Is.EqualTo(AbdmDataFlowRules.ConsentNotGrantedCode));
            _push.Verify(p => p.PushAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task ARequestWithAPushUrlIntoOurOwnNetwork_IsRefused()
        {
            await GrantAsync(ConsentBody());
            _calls.Clear();

            await Service().OnHealthInformationRequestAsync(Inbound("req-data", RequestBody(url: "https://10.0.0.5/push")), CancellationToken.None);

            Assert.That(Body(_calls.Single()).GetProperty("error").GetProperty("code").GetString(), Is.EqualTo(AbdmDataFlowRules.InvalidRequestCode));
            _push.Verify(p => p.PushAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task ACareContextTheConsentDoesNotName_IsNotSent()
        {
            await GrantAsync(ConsentBody(reference: "OPD-some-other-visit"));
            _calls.Clear();

            await Service().OnHealthInformationRequestAsync(Inbound("req-data", RequestBody()), CancellationToken.None);

            _records.Verify(r => r.BuildAsync(It.IsAny<AbdmCareContext>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()), Times.Never);
            Assert.That(JsonDocument.Parse(_pushedJson!).RootElement.GetProperty("entries").GetArrayLength(), Is.EqualTo(0));
        }

        [Test]
        public async Task AVisitOutsideTheDatesAskedFor_IsNotSent()
        {
            await GrantAsync(ConsentBody());

            await Service().OnHealthInformationRequestAsync(Inbound("req-data", RequestBody(from: "2026-11-01T00:00:00.000Z", to: "2026-11-30T00:00:00.000Z")), CancellationToken.None);

            Assert.That(JsonDocument.Parse(_pushedJson!).RootElement.GetProperty("entries").GetArrayLength(), Is.EqualTo(0));
        }

        [Test]
        public async Task WhenTheHiuDoesNotAcceptThePush_AbdmIsToldItFailed()
        {
            await GrantAsync(ConsentBody());
            _calls.Clear();
            _pushStatus = 500;

            await Service().OnHealthInformationRequestAsync(Inbound("req-data", RequestBody()), CancellationToken.None);

            var status = Body(_calls.Last()).GetProperty("notification").GetProperty("statusNotification");
            Assert.That(status.GetProperty("sessionStatus").GetString(), Is.EqualTo("FAILED"));
            Assert.That(status.GetProperty("statusResponses")[0].GetProperty("hiStatus").GetString(), Is.EqualTo("ERRORED"));
            Assert.That(_db.AbdmDataTransfers.Single().Status, Is.EqualTo(AbdmTransferStatus.Failed));
        }
    
        // ---------------------------------------------------------------- pages

        [TestCase(0, 1, true)]
        [TestCase(1, 1, true)]
        [TestCase(1, 3, false)]
        [TestCase(2, 3, true, Description = "last page when counted from 0")]
        [TestCase(3, 3, true, Description = "last page when counted from 1")]
        public void TheLastPage_IsKnown_WhicheverWayTheSenderCounts(int pageNumber, int pageCount, bool last) =>
            Assert.That(AbdmDataFlowRules.IsLastPage(pageNumber, pageCount), Is.EqualTo(last));

        [Test]
        public void ATransfer_IsCutIntoPages_ByCountAndBySize()
        {
            Assert.That(AbdmDataFlowRules.Pages(Array.Empty<int>()), Is.EqualTo(new[] { (0, 0) }), "nothing to send is still one page");
            Assert.That(AbdmDataFlowRules.Pages(new[] { 10, 10, 10 }), Is.EqualTo(new[] { (0, 3) }));
            Assert.That(AbdmDataFlowRules.Pages(new[] { 10, 10, 10, 10, 10 }, maxEntries: 2), Is.EqualTo(new[] { (0, 2), (2, 2), (4, 1) }));
            Assert.That(AbdmDataFlowRules.Pages(new[] { 60, 60, 30, 200, 10 }, maxEntries: 20, maxChars: 100), Is.EqualTo(new[] { (0, 1), (1, 2), (3, 1), (4, 1) }),
                "a record larger than the limit goes alone");
        }

        [Test]
        public async Task ManyRecords_GoAsNumberedPages_EachWithTheSameKey_AndAllCanBeRead()
        {
            var many = Enumerable.Range(1, AbdmDataFlowRules.PageMaxEntries + 5).Select(i => new AbdmBuiltRecord("Prescription", $"{{\"n\":{i}}}")).ToArray();
            _records.Setup(r => r.BuildAsync(It.IsAny<AbdmCareContext>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new AbdmBuiltRecords { WhenUtc = _visitDate, Records = many });
            var pushed = new List<string>();
            _push.Setup(p => p.PushAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string _, string json, CancellationToken _) => { pushed.Add(json); return 202; });
            await GrantAsync(ConsentBody());

            await Service().OnHealthInformationRequestAsync(Inbound("req-data", RequestBody()), CancellationToken.None);

            Assert.That(pushed.Count, Is.EqualTo(2));
            var pages = pushed.Select(p => JsonDocument.Parse(p).RootElement).ToList();
            Assert.That(pages.Select(p => p.GetProperty("pageNumber").GetInt32()), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(pages.Select(p => p.GetProperty("pageCount").GetInt32()), Is.EqualTo(new[] { 2, 2 }));
            Assert.That(pages.Select(p => p.GetProperty("entries").GetArrayLength()), Is.EqualTo(new[] { AbdmDataFlowRules.PageMaxEntries, 5 }));
            Assert.That(pages[0].GetProperty("keyMaterial").GetRawText(), Is.EqualTo(pages[1].GetProperty("keyMaterial").GetRawText()));

            var keys = pages[1].GetProperty("keyMaterial");
            var last = AbdmFidelius.Decrypt(pages[1].GetProperty("entries")[4].GetProperty("content").GetString()!, HiuPrivate, HiuNonce,
                keys.GetProperty("dhPublicKey").GetProperty("keyValue").GetString()!, keys.GetProperty("nonce").GetString()!);
            Assert.That(last, Is.EqualTo($"{{\"n\":{AbdmDataFlowRules.PageMaxEntries + 5}}}"));
            Assert.That(_db.AbdmDataTransfers.Single().RecordCount, Is.EqualTo(AbdmDataFlowRules.PageMaxEntries + 5));
        }

        [Test]
        public async Task WhenALaterPageIsNotAccepted_TheTransferIsReportedFailed()
        {
            var many = Enumerable.Range(1, AbdmDataFlowRules.PageMaxEntries + 1).Select(i => new AbdmBuiltRecord("Prescription", $"{{\"n\":{i}}}")).ToArray();
            _records.Setup(r => r.BuildAsync(It.IsAny<AbdmCareContext>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new AbdmBuiltRecords { WhenUtc = _visitDate, Records = many });
            var sent = 0;
            _push.Setup(p => p.PushAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => ++sent == 1 ? 202 : 500);
            await GrantAsync(ConsentBody());
            _calls.Clear();

            await Service().OnHealthInformationRequestAsync(Inbound("req-data", RequestBody()), CancellationToken.None);

            var transfer = _db.AbdmDataTransfers.Single();
            Assert.That(transfer.Status, Is.EqualTo(AbdmTransferStatus.Failed));
            Assert.That(transfer.ErrorMessage, Does.Contain("page 2 of 2"));
            Assert.That(Body(_calls.Last()).GetProperty("notification").GetProperty("statusNotification").GetProperty("sessionStatus").GetString(), Is.EqualTo("FAILED"));
        }
    }
}
