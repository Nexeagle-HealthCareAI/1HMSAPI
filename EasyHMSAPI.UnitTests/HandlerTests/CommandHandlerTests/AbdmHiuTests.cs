#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    // ABDM M3: this hospital as a Health Information User. Ask for consent, follow it to a
    // granted artefact, ask for the records with a one-time key, take them in at a one-time
    // address, and keep them only as long as the consent allows.
    [TestFixture]
    public class AbdmHiuTests
    {
        private const string PatientId = "P-0001";
        private const string AbhaAddress = "asha@sbx";
        private const string Bundle = "{\"resourceType\":\"Bundle\",\"entry\":[{\"resource\":{\"resourceType\":\"Composition\",\"title\":\"Prescription\",\"meta\":{\"profile\":[\"https://nrces.in/ndhm/fhir/r4/StructureDefinition/PrescriptionRecord\"]}}}]}";

        private AppDbContext _db = null!;
        private Mock<IAbdmHieService> _hie = null!;
        private List<AbdmHieCall> _calls = null!;
        private Guid _hospitalId;

        [SetUp]
        public void SetUp()
        {
            _db = InMemoryDbContextFactory.CreateContext();
            _hospitalId = Guid.NewGuid();
            _db.AbdmFacilities.Add(new AbdmFacility { HospitalId = _hospitalId, HipId = "IN2810014366", UpdatedAt = DateTime.UtcNow });
            _db.PatientRegistrations.Add(new PatientRegistration { RegistrationId = Guid.NewGuid(), HospitalId = _hospitalId, PatientId = PatientId, FullName = "Asha Rao", AbhaId = AbhaAddress });
            _db.SaveChanges();

            _calls = new List<AbdmHieCall>();
            _hie = new Mock<IAbdmHieService>();
            _hie.Setup(h => h.SendAsync(It.IsAny<AbdmHieCall>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((AbdmHieCall call, CancellationToken _) =>
                {
                    _calls.Add(call);
                    var exchange = new AbdmExchange
                    {
                        ExchangeId = Guid.NewGuid(), RequestId = Guid.NewGuid().ToString(), HospitalId = call.HospitalId, FacilityId = "IN2810014366",
                        Kind = call.Kind, Path = call.Path, SubjectRef = call.SubjectRef, ExpectsCallback = call.ExpectsCallback,
                        Status = AbdmExchangeStatus.Accepted, SentAt = DateTime.UtcNow
                    };
                    _db.AbdmExchanges.Add(exchange);
                    _db.SaveChanges();
                    return new AbdmHieResult { ExchangeId = exchange.ExchangeId, RequestId = exchange.RequestId, HttpStatus = 202 };
                });
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_db);
            _db.Dispose();
        }

        private AbdmHiuService Service(string? publicBase = "https://api.example.org")
        {
            var configuration = new Mock<IConfiguration>();
            configuration.Setup(c => c["Abdm:PublicBaseUrl"]).Returns(publicBase);
            return new AbdmHiuService(_db, _hie.Object, configuration.Object, NullLogger<AbdmHiuService>.Instance);
        }

        private AbdmHiuConsentAsk Ask(IReadOnlyList<string>? hiTypes = null) => new()
        {
            HospitalId = _hospitalId, PatientId = PatientId, PurposeCode = "CAREMGT", HiTypes = hiTypes ?? new[] { "Prescription", "DiagnosticReport" },
            From = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), To = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
            EraseAt = DateTime.UtcNow.AddDays(30), RequesterName = "Dr Meera Iyer"
        };

        private static JsonElement Body(AbdmHieCall call) => JsonDocument.Parse(JsonSerializer.Serialize(call.Body)).RootElement;

        private AbdmInbound Answer(string kind, object body)
        {
            var exchange = _db.AbdmExchanges.OrderByDescending(e => e.SentAt).First(e => e.Kind == kind);
            return new AbdmInbound { InboundId = Guid.NewGuid(), HospitalId = _hospitalId, AnswersRequestId = exchange.RequestId, RawBody = JsonSerializer.Serialize(body), Verdict = AbdmInboundVerdict.Accepted };
        }

        private AbdmInbound Notice(string requestId, object body) => new()
        {
            InboundId = Guid.NewGuid(), HospitalId = _hospitalId, HeaderRequestId = requestId, RawBody = JsonSerializer.Serialize(body), Verdict = AbdmInboundVerdict.Accepted
        };

        /// <summary>Takes a request all the way to "records asked for"; returns the transfer.</summary>
        private async Task<AbdmDataTransfer> GrantedAndRequestedAsync(DateTime? eraseAt = null)
        {
            var service = Service();
            await service.RequestConsentAsync(Ask(), CancellationToken.None);
            await service.OnConsentInitAnsweredAsync(Answer("consent-init", new { consentRequest = new { id = "creq-1" }, response = new { requestId = "x" } }), CancellationToken.None);
            await service.OnConsentNotifyAsync(Notice("req-notify", new { notification = new { consentRequestId = "creq-1", status = "GRANTED", consentArtefacts = new[] { new { id = "art-1" } } } }), CancellationToken.None);
            await service.OnConsentFetchedAsync(Answer("consent-fetch", new
            {
                consent = new
                {
                    status = "GRANTED",
                    consentDetail = new
                    {
                        consentId = "art-1",
                        hip = new { id = "hip-7", name = "Other Hospital" },
                        careContexts = new[] { new { patientReference = "X-1", careContextReference = "EP-1" } },
                        hiTypes = new[] { "Prescription" },
                        permission = new { dateRange = new { from = "2025-01-01T00:00:00.000Z", to = "2026-11-01T00:00:00.000Z" }, dataEraseAt = (eraseAt ?? DateTime.UtcNow.AddDays(30)).ToString("yyyy-MM-ddTHH:mm:ss.fffZ") }
                    }
                },
                response = new { requestId = "x" }
            }), CancellationToken.None);
            await service.OnDataRequestAnsweredAsync(Answer("hi-request", new { hiRequest = new { transactionId = "txn-5", sessionStatus = "REQUESTED" }, response = new { requestId = "x" } }), CancellationToken.None);
            return _db.AbdmDataTransfers.Single();
        }

        /// <summary>What the other facility would push: the bundle encrypted for our key.</summary>
        private string Push(AbdmDataTransfer transfer, string? content = null, int pageNumber = 1, int pageCount = 1)
        {
            var request = Body(_calls.Single(c => c.Kind == "hi-request")).GetProperty("hiRequest").GetProperty("keyMaterial");
            var hip = AbdmFidelius.NewKeyMaterial();
            var encrypted = AbdmFidelius.Encrypt(Bundle, hip.PrivateKey, hip.Nonce,
                request.GetProperty("dhPublicKey").GetProperty("keyValue").GetString()!, request.GetProperty("nonce").GetString()!);
            return JsonSerializer.Serialize(new
            {
                pageNumber, pageCount, transactionId = transfer.TransactionId,
                entries = new[] { new { content = content ?? encrypted, media = "application/fhir+json", checksum = "x", careContextReference = "EP-1" } },
                keyMaterial = new { cryptoAlg = "ECDH", curve = "curve25519", dhPublicKey = new { expiry = "2099-01-01T00:00:00.000Z", parameters = "x", keyValue = hip.PublicKey }, nonce = hip.Nonce }
            });
        }

        // ---------------------------------------------------------------- asking

        [Test]
        public async Task AskingForConsent_SendsTheRequestAsTheHiu_AndRemembersIt()
        {
            var consent = await Service().RequestConsentAsync(Ask(), CancellationToken.None);

            var call = _calls.Single();
            Assert.That(call.Path, Does.EndWith("consent/v3/request/init"));
            Assert.That(call.FacilityHeader, Is.EqualTo(AbdmFacilityHeader.Hiu));
            var body = Body(call).GetProperty("consent");
            Assert.That(body.GetProperty("patient").GetProperty("id").GetString(), Is.EqualTo(AbhaAddress));
            Assert.That(body.GetProperty("purpose").GetProperty("code").GetString(), Is.EqualTo("CAREMGT"));
            Assert.That(body.GetProperty("hiu").GetProperty("id").GetString(), Is.EqualTo("IN2810014366"));
            Assert.That(body.GetProperty("hiTypes").GetArrayLength(), Is.EqualTo(2));
            Assert.That(body.GetProperty("permission").GetProperty("accessMode").GetString(), Is.EqualTo("VIEW"));

            Assert.That(consent.Role, Is.EqualTo(AbdmConsentRole.Hiu));
            Assert.That(consent.Status, Is.EqualTo(AbdmConsentStatus.Requested));
            Assert.That(_db.AbdmConsents.Single().PatientId, Is.EqualTo(PatientId));
        }

        [Test]
        public void AskingWithoutAnAbhaAddress_OrWithNothingToAskFor_IsRefusedBeforeAnythingIsSent()
        {
            Assert.ThrowsAsync<AbdmHiuRefusedException>(() => Service().RequestConsentAsync(Ask(hiTypes: new[] { "NotARecordType" }), CancellationToken.None));

            _db.PatientRegistrations.Single().AbhaId = null;
            _db.SaveChanges();
            var ex = Assert.ThrowsAsync<AbdmHiuRefusedException>(() => Service().RequestConsentAsync(Ask(), CancellationToken.None));

            Assert.That(ex!.Message, Does.Contain("ABHA address"));
            Assert.That(_calls, Is.Empty);
            Assert.That(_db.AbdmConsents.Any(), Is.False);
        }

        // ---------------------------------------------------------------- the journey

        [Test]
        public async Task AGrant_IsAcknowledged_Fetched_AndTheRecordsAreAskedForWithAOneTimeKeyAndAddress()
        {
            var transfer = await GrantedAndRequestedAsync();

            Assert.That(_calls.Select(c => c.Kind), Is.EqualTo(new[] { "consent-init", "hiu-consent-on-notify", "consent-fetch", "hi-request" }));
            var ack = Body(_calls[1]);
            Assert.That(ack.GetProperty("acknowledgement")[0].GetProperty("consentId").GetString(), Is.EqualTo("art-1"));
            Assert.That(ack.GetProperty("response").GetProperty("requestId").GetString(), Is.EqualTo("req-notify"));

            var consent = _db.AbdmConsents.Single();
            Assert.That(consent.ConsentId, Is.EqualTo("art-1"));
            Assert.That(consent.Status, Is.EqualTo(AbdmConsentStatus.Granted));
            Assert.That(consent.CounterpartyName, Is.EqualTo("Other Hospital"));
            Assert.That(consent.CareContextReferences, Is.EqualTo("EP-1"));

            var request = Body(_calls[3]).GetProperty("hiRequest");
            Assert.That(request.GetProperty("consent").GetProperty("id").GetString(), Is.EqualTo("art-1"));
            Assert.That(request.GetProperty("dataPushUrl").GetString(), Is.EqualTo($"https://api.example.org/abdm-hiu/data-push/{transfer.PushToken}"));
            Assert.That(transfer.PushToken, Has.Length.EqualTo(64));
            Assert.That(request.GetProperty("keyMaterial").GetProperty("dhPublicKey").GetProperty("keyValue").GetString(), Is.EqualTo(AbdmFidelius.PublicKeyFor(transfer.PrivateKey!)));
            Assert.That(JsonSerializer.Serialize(_calls[3].Body), Does.Not.Contain(transfer.PrivateKey!), "the private key never leaves");
            Assert.That(transfer.TransactionId, Is.EqualTo("txn-5"));
        }

        [Test]
        public async Task ADenial_IsRecorded_AndNothingIsFetched()
        {
            var service = Service();
            await service.RequestConsentAsync(Ask(), CancellationToken.None);
            await service.OnConsentInitAnsweredAsync(Answer("consent-init", new { consentRequest = new { id = "creq-1" }, response = new { requestId = "x" } }), CancellationToken.None);

            await service.OnConsentNotifyAsync(Notice("req-notify", new { notification = new { consentRequestId = "creq-1", status = "DENIED" } }), CancellationToken.None);

            Assert.That(_db.AbdmConsents.Single().Status, Is.EqualTo(AbdmConsentStatus.Denied));
            Assert.That(_calls.Any(c => c.Kind == "consent-fetch"), Is.False);
        }

        [Test]
        public async Task WithNoPublicAddressConfigured_TheRecordsAreNotAskedFor_AndTheReasonIsKept()
        {
            var service = Service(publicBase: null);
            await service.RequestConsentAsync(Ask(), CancellationToken.None);
            await service.OnConsentInitAnsweredAsync(Answer("consent-init", new { consentRequest = new { id = "creq-1" }, response = new { requestId = "x" } }), CancellationToken.None);
            await service.OnConsentNotifyAsync(Notice("n", new { notification = new { consentRequestId = "creq-1", status = "GRANTED", consentArtefacts = new[] { new { id = "art-1" } } } }), CancellationToken.None);
            await service.OnConsentFetchedAsync(Answer("consent-fetch", new { consent = new { status = "GRANTED", consentDetail = new { consentId = "art-1" } }, response = new { requestId = "x" } }), CancellationToken.None);

            Assert.That(_calls.Any(c => c.Kind == "hi-request"), Is.False);
            var transfer = _db.AbdmDataTransfers.Single();
            Assert.That(transfer.Status, Is.EqualTo(AbdmTransferStatus.Failed));
            Assert.That(transfer.ErrorMessage, Does.Contain("Abdm:PublicBaseUrl"));
        }

        // ---------------------------------------------------------------- receiving

        [Test]
        public async Task RecordsPushedToTheOneTimeAddress_AreDecryptedAndKept_AndTheKeyIsThrownAway()
        {
            var transfer = await GrantedAndRequestedAsync();
            var token = transfer.PushToken!;

            var accepted = await Service().ReceiveDataAsync(token, Push(transfer), CancellationToken.None);

            Assert.That(accepted, Is.True);
            var record = _db.AbdmReceivedRecords.Single();
            Assert.That(record.BundleJson, Is.EqualTo(Bundle));
            Assert.That(record.PatientId, Is.EqualTo(PatientId));
            Assert.That(record.Title, Is.EqualTo("Prescription"));
            Assert.That(record.HiType, Is.EqualTo("Prescription"));
            Assert.That(record.CareContextReference, Is.EqualTo("EP-1"));
            Assert.That(record.EraseAt, Is.Not.Null);

            var after = _db.AbdmDataTransfers.Single();
            Assert.That(after.Status, Is.EqualTo(AbdmTransferStatus.Transferred));
            Assert.That(after.PrivateKey, Is.Null);
            Assert.That(after.PushToken, Is.Null);

            var notify = Body(_calls.Last()).GetProperty("notification");
            Assert.That(_calls.Last().Path, Does.EndWith("health-information/notify"));
            Assert.That(notify.GetProperty("notifier").GetProperty("type").GetString(), Is.EqualTo("HIU"));
            Assert.That(notify.GetProperty("statusNotification").GetProperty("statusResponses")[0].GetProperty("hiStatus").GetString(), Is.EqualTo("OK"));

            // The address worked once.
            Assert.That(await Service().ReceiveDataAsync(token, Push(transfer), CancellationToken.None), Is.False);
            Assert.That(_db.AbdmReceivedRecords.Count(), Is.EqualTo(1));
        }

        [Test]
        public async Task AnUnknownAddress_TakesNothing()
        {
            var transfer = await GrantedAndRequestedAsync();

            Assert.That(await Service().ReceiveDataAsync("not-the-token", Push(transfer), CancellationToken.None), Is.False);
            Assert.That(_db.AbdmReceivedRecords.Any(), Is.False);
        }

        [Test]
        public async Task ContentThatDoesNotDecrypt_IsNotKept_AndTheTransferIsMarkedFailed()
        {
            var transfer = await GrantedAndRequestedAsync();

            await Service().ReceiveDataAsync(transfer.PushToken!, Push(transfer, content: Convert.ToBase64String(new byte[64])), CancellationToken.None);

            Assert.That(_db.AbdmReceivedRecords.Any(), Is.False);
            Assert.That(_db.AbdmDataTransfers.Single().Status, Is.EqualTo(AbdmTransferStatus.Failed));
            var status = Body(_calls.Last()).GetProperty("notification").GetProperty("statusNotification");
            Assert.That(status.GetProperty("statusResponses")[0].GetProperty("hiStatus").GetString(), Is.EqualTo("ERRORED"));
        }

        // ---------------------------------------------------------------- keeping and erasing

        [Test]
        public async Task ARevocation_RemovesWhatWasReceivedUnderThatConsent()
        {
            var transfer = await GrantedAndRequestedAsync();
            await Service().ReceiveDataAsync(transfer.PushToken!, Push(transfer), CancellationToken.None);
            Assert.That(_db.AbdmReceivedRecords.Count(), Is.EqualTo(1));

            await Service().OnConsentNotifyAsync(Notice("req-revoke", new { notification = new { consentRequestId = "creq-1", status = "REVOKED", consentArtefacts = new[] { new { id = "art-1" } } } }), CancellationToken.None);

            Assert.That(_db.AbdmReceivedRecords.Any(), Is.False);
            Assert.That(_db.AbdmConsents.Single().Status, Is.EqualTo(AbdmConsentStatus.Revoked));
        }

        [Test]
        public async Task RecordsPastTheirEraseDate_AreDeleted_AndCannotBeOpened()
        {
            var transfer = await GrantedAndRequestedAsync();
            await Service().ReceiveDataAsync(transfer.PushToken!, Push(transfer), CancellationToken.None);
            var record = _db.AbdmReceivedRecords.Single();
            Assert.That((await Service().ListRecordsAsync(_hospitalId, PatientId, CancellationToken.None)).Single().FromFacility, Is.EqualTo("Other Hospital"));

            record.EraseAt = DateTime.UtcNow.AddMinutes(-1);
            _db.SaveChanges();

            Assert.That(await Service().GetRecordAsync(_hospitalId, record.ReceivedRecordId, CancellationToken.None), Is.Null);
            Assert.That(await Service().ListRecordsAsync(_hospitalId, PatientId, CancellationToken.None), Is.Empty);
            Assert.That(_db.AbdmReceivedRecords.Any(), Is.False);
        }

        [Test]
        public async Task TheSweep_ErasesWhatIsPastItsDate_AndClosesTransfersThatNeverReceivedAnything()
        {
            var transfer = await GrantedAndRequestedAsync();
            await Service().ReceiveDataAsync(transfer.PushToken!, Push(transfer), CancellationToken.None);
            var kept = _db.AbdmReceivedRecords.Single();
            var other = Guid.NewGuid();
            _db.AbdmReceivedRecords.Add(new AbdmReceivedRecord
            {
                ReceivedRecordId = Guid.NewGuid(), HospitalId = other, ConsentId = "c-old", TransactionId = "t-old", PatientId = "X",
                BundleJson = Bundle, ReceivedAt = DateTime.UtcNow.AddDays(-40), EraseAt = DateTime.UtcNow.AddMinutes(-5)
            });
            var stale = new AbdmDataTransfer
            {
                TransferId = Guid.NewGuid(), HospitalId = other, Role = AbdmConsentRole.Hiu, ConsentId = "c-stale", TransactionId = "t-stale",
                Status = AbdmTransferStatus.Requested, PrivateKey = "key", Nonce = "nonce", PushToken = "token", RequestedAt = DateTime.UtcNow.AddHours(-25)
            };
            var fresh = new AbdmDataTransfer
            {
                TransferId = Guid.NewGuid(), HospitalId = other, Role = AbdmConsentRole.Hiu, ConsentId = "c-fresh", TransactionId = "t-fresh",
                Status = AbdmTransferStatus.Requested, PrivateKey = "key", Nonce = "nonce", PushToken = "token2", RequestedAt = DateTime.UtcNow.AddHours(-1)
            };
            _db.AbdmDataTransfers.AddRange(stale, fresh);
            _db.SaveChanges();

            // Every hospital is swept, without anyone opening a list.
            var sweep = await Service().SweepAsync(DateTime.UtcNow, CancellationToken.None);

            Assert.That(sweep, Is.EqualTo(new AbdmHiuSweep(1, 1)));
            Assert.That(_db.AbdmReceivedRecords.Select(r => r.ReceivedRecordId), Is.EqualTo(new[] { kept.ReceivedRecordId }));
            Assert.That(stale.Status, Is.EqualTo(AbdmTransferStatus.Failed));
            Assert.That(new[] { stale.PrivateKey, stale.Nonce, stale.PushToken }, Is.All.Null, "the key and the address are thrown away");
            Assert.That(await Service().ReceiveDataAsync("token", "{}", CancellationToken.None), Is.False, "the closed address takes nothing");
            Assert.That(fresh.Status, Is.EqualTo(AbdmTransferStatus.Requested));
            Assert.That(fresh.PrivateKey, Is.EqualTo("key"));
        }

        [Test]
        public async Task ATransferCountedFrom1_StaysOpenUntilItsLastPage()
        {
            var transfer = await GrantedAndRequestedAsync();
            var token = transfer.PushToken!;

            Assert.That(await Service().ReceiveDataAsync(token, Push(transfer, pageNumber: 1, pageCount: 3), CancellationToken.None), Is.True);
            Assert.That(await Service().ReceiveDataAsync(token, Push(transfer, pageNumber: 2, pageCount: 3), CancellationToken.None), Is.True);
            Assert.That(transfer.Status, Is.EqualTo(AbdmTransferStatus.Requested), "page 2 of 3 is not the end");
            Assert.That(await Service().ReceiveDataAsync(token, Push(transfer, pageNumber: 3, pageCount: 3), CancellationToken.None), Is.True);

            Assert.That(transfer.Status, Is.EqualTo(AbdmTransferStatus.Transferred));
            Assert.That(_db.AbdmReceivedRecords.Count(), Is.EqualTo(3));
            Assert.That(transfer.PushToken, Is.Null, "the address closes after the last page");
        }

        [Test]
        public async Task TheSweep_KeepsWhatArrived_WhenASenderNeverSentAnNofNPage()
        {
            var transfer = await GrantedAndRequestedAsync();
            // Counted from 0: page 1 of 3 is the second page, and the third never says "3 of 3".
            await Service().ReceiveDataAsync(transfer.PushToken!, Push(transfer, pageNumber: 0, pageCount: 3), CancellationToken.None);
            await Service().ReceiveDataAsync(transfer.PushToken!, Push(transfer, pageNumber: 2, pageCount: 3), CancellationToken.None);
            Assert.That(transfer.Status, Is.EqualTo(AbdmTransferStatus.Requested));

            var sweep = await Service().SweepAsync(DateTime.UtcNow.AddHours(25), CancellationToken.None);

            Assert.That(sweep.TransfersClosed, Is.EqualTo(1));
            Assert.That(transfer.Status, Is.EqualTo(AbdmTransferStatus.Transferred));
            Assert.That(transfer.ErrorMessage, Is.Null);
            Assert.That(_db.AbdmReceivedRecords.Count(), Is.EqualTo(2), "nothing that arrived is lost");
            Assert.That(new[] { transfer.PrivateKey, transfer.Nonce, transfer.PushToken }, Is.All.Null);
        }

        [Test]
        public async Task AnotherHospital_SeesNoneOfIt()
        {
            var transfer = await GrantedAndRequestedAsync();
            await Service().ReceiveDataAsync(transfer.PushToken!, Push(transfer), CancellationToken.None);
            var other = Guid.NewGuid();

            Assert.That(await Service().ListRecordsAsync(other, PatientId, CancellationToken.None), Is.Empty);
            Assert.That(await Service().GetRecordAsync(other, _db.AbdmReceivedRecords.Single().ReceivedRecordId, CancellationToken.None), Is.Null);
            Assert.That(await Service().ListConsentsAsync(other, null, CancellationToken.None), Is.Empty);
        }
    
        // ---------------------------------------------------------------- checking where a request stands

        private AbdmHiuService ServiceWith(Mock<IAbdmDataPushClient> push)
        {
            var configuration = new Mock<IConfiguration>();
            configuration.Setup(c => c["Abdm:PublicBaseUrl"]).Returns("https://api.example.org");
            return new AbdmHiuService(_db, _hie.Object, configuration.Object, NullLogger<AbdmHiuService>.Instance, push.Object);
        }

        private async Task<AbdmConsent> AskedAsync()
        {
            var service = Service();
            await service.RequestConsentAsync(Ask(), CancellationToken.None);
            await service.OnConsentInitAnsweredAsync(Answer("consent-init", new { consentRequest = new { id = "creq-1" }, response = new { requestId = "x" } }), CancellationToken.None);
            return _db.AbdmConsents.Single();
        }

        [Test]
        public async Task AStatusAnswerThatCarriesArtefacts_IsFollowedUp_AndTheRecordsAreAskedForOnce()
        {
            var consent = await AskedAsync();
            _calls.Clear();

            var check = await Service().CheckAsync(_hospitalId, consent.ConsentRowId, CancellationToken.None);

            Assert.That(check.ConsentAsked, Is.True);
            Assert.That(check.TransferStatus, Is.Null);
            var ask = _calls.Single();
            Assert.That(ask.Path, Does.EndWith("consent/v3/request/status"));
            Assert.That(ask.FacilityHeader, Is.EqualTo(AbdmFacilityHeader.Hiu));
            Assert.That(Body(ask).GetProperty("consentRequestId").GetString(), Is.EqualTo("creq-1"));

            var answer = Answer("consent-status", new { consentRequest = new { id = "creq-1", status = "GRANTED", consentArtefacts = new[] { new { id = "art-1" } } }, response = new { requestId = "x" } });
            await Service().OnConsentStatusAnsweredAsync(answer, CancellationToken.None);

            var row = _db.AbdmConsents.Single();
            Assert.That(row.Status, Is.EqualTo(AbdmConsentStatus.Granted));
            Assert.That(row.ConsentId, Is.EqualTo("art-1"));
            Assert.That(_calls.Count(c => c.Kind == "consent-fetch"), Is.EqualTo(1));

            // The same answer again (a second check) fetches nothing twice.
            await Service().OnConsentStatusAnsweredAsync(answer, CancellationToken.None);
            Assert.That(_calls.Count(c => c.Kind == "consent-fetch"), Is.EqualTo(1));
        }

        [TestCase("REQUESTED", AbdmConsentStatus.Requested, Description = "the patient has not answered: nothing changes")]
        [TestCase("GRANTED", AbdmConsentStatus.Requested, Description = "not an answer ABDM lists here, and no artefact to fetch: nothing changes")]
        [TestCase("REVOKED", AbdmConsentStatus.Revoked)]
        [TestCase("DENIED", AbdmConsentStatus.Denied)]
        [TestCase("EXPIRED", AbdmConsentStatus.Expired)]
        public async Task AStatusAnswer_IsWrittenToTheRequest(string answered, string expected)
        {
            var consent = await AskedAsync();
            await Service().CheckAsync(_hospitalId, consent.ConsentRowId, CancellationToken.None);

            await Service().OnConsentStatusAnsweredAsync(Answer("consent-status", new { consentRequest = new { id = "creq-1", status = answered }, response = new { requestId = "x" } }), CancellationToken.None);

            Assert.That(_db.AbdmConsents.Single().Status, Is.EqualTo(expected));
            Assert.That(_calls.Any(c => c.Kind == "consent-fetch"), Is.False);
        }

        [Test]
        public async Task ACheck_AlsoReadsTheStateOfTheTransfer_WithAGet()
        {
            await GrantedAndRequestedAsync();
            _calls.Clear();
            _hie.Setup(h => h.SendAsync(It.Is<AbdmHieCall>(c => c.Kind == "hi-request-status"), It.IsAny<CancellationToken>()))
                .ReturnsAsync((AbdmHieCall call, CancellationToken _) =>
                {
                    _calls.Add(call);
                    return new AbdmHieResult { ExchangeId = Guid.NewGuid(), RequestId = "r", HttpStatus = 200, Body = "{\"transactionId\":\"txn-5\",\"status\":\"Transferred\"}" };
                });

            var check = await Service().CheckAsync(_hospitalId, _db.AbdmConsents.Single().ConsentRowId, CancellationToken.None);

            Assert.That(check.TransferStatus, Is.EqualTo("TRANSFERRED"));
            var status = _calls.Single(c => c.Kind == "hi-request-status");
            Assert.That(status.Method, Is.EqualTo(System.Net.Http.HttpMethod.Get));
            Assert.That(status.Path, Does.EndWith("health-information/request/status/txn-5"));
        }

        [Test]
        public void CheckingAnotherHospitalsRequest_IsRefused()
        {
            Assert.ThrowsAsync<AbdmHiuRefusedException>(() => Service().CheckAsync(_hospitalId, Guid.NewGuid(), CancellationToken.None));
            Assert.That(_calls, Is.Empty);
        }

        // ---------------------------------------------------------------- a record sent as a link

        [Test]
        public async Task ARecordSentAsALink_IsFetched_Decrypted_AndKept()
        {
            var transfer = await GrantedAndRequestedAsync();
            var inline = JsonDocument.Parse(Push(transfer)).RootElement;
            var encrypted = inline.GetProperty("entries")[0].GetProperty("content").GetString()!;
            var body = JsonSerializer.Serialize(new
            {
                pageNumber = 1, pageCount = 1, transactionId = transfer.TransactionId,
                entries = new[] { new { link = "https://records.other-hospital.example/r/1", media = "application/fhir+json", checksum = "x", careContextReference = "EP-1" } },
                keyMaterial = JsonSerializer.Deserialize<object>(inline.GetProperty("keyMaterial").GetRawText())
            });
            var push = new Mock<IAbdmDataPushClient>();
            push.Setup(p => p.FetchAsync("https://records.other-hospital.example/r/1", It.IsAny<CancellationToken>())).ReturnsAsync(encrypted);

            Assert.That(await ServiceWith(push).ReceiveDataAsync(transfer.PushToken!, body, CancellationToken.None), Is.True);

            Assert.That(_db.AbdmReceivedRecords.Single().BundleJson, Is.EqualTo(Bundle));
            Assert.That(_db.AbdmDataTransfers.Single().Status, Is.EqualTo(AbdmTransferStatus.Transferred));
        }

        [Test]
        public async Task ALinkThatCannotBeRead_IsReportedAsNotReceived_AndNothingIsKept()
        {
            var transfer = await GrantedAndRequestedAsync();
            var inline = JsonDocument.Parse(Push(transfer)).RootElement;
            var body = JsonSerializer.Serialize(new
            {
                pageNumber = 1, pageCount = 1, transactionId = transfer.TransactionId,
                entries = new[] { new { link = "https://10.0.0.5/r/1", media = "application/fhir+json", checksum = "x", careContextReference = "EP-1" } },
                keyMaterial = JsonSerializer.Deserialize<object>(inline.GetProperty("keyMaterial").GetRawText())
            });
            var push = new Mock<IAbdmDataPushClient>();
            push.Setup(p => p.FetchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);
            _calls.Clear();

            await ServiceWith(push).ReceiveDataAsync(transfer.PushToken!, body, CancellationToken.None);

            Assert.That(_db.AbdmReceivedRecords.Any(), Is.False);
            Assert.That(_db.AbdmDataTransfers.Single().Status, Is.EqualTo(AbdmTransferStatus.Failed));
            var told = Body(_calls.Single(c => c.Kind == "hiu-hi-notify")).GetProperty("notification").GetProperty("statusNotification");
            Assert.That(told.GetProperty("statusResponses")[0].GetProperty("hiStatus").GetString(), Is.EqualTo("ERRORED"));
        }
    }
}
