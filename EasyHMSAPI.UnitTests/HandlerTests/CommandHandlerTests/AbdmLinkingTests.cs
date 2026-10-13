using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    // ABDM M2 phase 2: HIP-initiated linking. generate-token → on-generate-token → link/carecontext →
    // on_carecontext, with every step's state on the care context row and nothing sent twice.
    [TestFixture]
    public class AbdmLinkingTests
    {
        private AppDbContext _db = null!;
        private Mock<IAbdmHieService> _hie = null!;
        private List<AbdmHieCall> _calls = null!;
        private Guid _hospitalId, _apptId;
        private const string HipId = "IN2810014366";
        private const string PatientId = "P-0001";
        private const string AbhaAddress = "asha@sbx";

        [SetUp]
        public void SetUp()
        {
            _db = InMemoryDbContextFactory.CreateContext();
            _hospitalId = Guid.NewGuid();
            _apptId = Guid.NewGuid();
            _db.AbdmFacilities.Add(new AbdmFacility { HospitalId = _hospitalId, HipId = HipId, UpdatedAt = DateTime.UtcNow });
            _db.Appointments.Add(new Appointment { ApptId = _apptId, HospitalId = _hospitalId, PatientId = PatientId, ApptDate = new DateTime(2026, 10, 3, 5, 0, 0, DateTimeKind.Utc) });
            _db.PatientRegistrations.Add(new PatientRegistration { RegistrationId = Guid.NewGuid(), HospitalId = _hospitalId, PatientId = PatientId, FullName = "Asha Rao", Sex = "Female", AbhaId = "91-1234-5678-9012" });
            _db.AbhaAccount.Add(new AbhaAccount { AbhaAccountId = Guid.NewGuid(), HospitalId = _hospitalId, AbhaNumber = "91-1234-5678-9012", AbhaAddress = AbhaAddress, FullName = "Asha Rao", Gender = "F", DateOfBirth = "15-08-1990", CreatedAt = DateTime.UtcNow });
            _db.Prescription.Add(new Prescription { PrescriptionId = Guid.NewGuid(), ApptId = _apptId, HospitalId = _hospitalId, PatientId = PatientId, CreatedAt = DateTime.UtcNow });
            _db.SaveChanges();

            _calls = new List<AbdmHieCall>();
            _hie = new Mock<IAbdmHieService>();
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_db);
            _db.Dispose();
        }

        private AbdmLinkingService Service() => new(_db, _hie.Object, NullLogger<AbdmLinkingService>.Instance);

        private static string Jwt(DateTime expiresUtc)
        {
            static string B64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            return $"{B64("{\"alg\":\"none\"}")}.{B64(JsonSerializer.Serialize(new { exp = new DateTimeOffset(expiresUtc).ToUnixTimeSeconds() }))}.sig";
        }

        /// <summary>Each fake send records a real exchange row, as the real client does, and
        /// returns it — so callbacks can be matched to it by REQUEST-ID.</summary>
        private void AcceptEverySend()
        {
            _hie.Setup(h => h.SendAsync(It.IsAny<AbdmHieCall>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((AbdmHieCall call, CancellationToken _) =>
                {
                    _calls.Add(call);
                    var exchange = new AbdmExchange
                    {
                        ExchangeId = Guid.NewGuid(), RequestId = Guid.NewGuid().ToString(), HospitalId = call.HospitalId, FacilityId = HipId,
                        Kind = call.Kind, Path = call.Path, SubjectRef = call.SubjectRef, ExpectsCallback = true,
                        Status = AbdmExchangeStatus.Accepted, SentAt = DateTime.UtcNow
                    };
                    _db.AbdmExchanges.Add(exchange);
                    _db.SaveChanges();
                    return new AbdmHieResult { ExchangeId = exchange.ExchangeId, RequestId = exchange.RequestId, HttpStatus = 202 };
                });
        }

        private AbdmInbound Answer(string kind, string body)
        {
            var exchange = _db.AbdmExchanges.Single(e => e.Kind == kind);
            return new AbdmInbound { InboundId = Guid.NewGuid(), AnswersRequestId = exchange.RequestId, RawBody = body, Verdict = AbdmInboundVerdict.Accepted };
        }

        private AbdmCareContext Row() => _db.AbdmCareContexts.Single();

        // ---------------------------------------------------------------- rules

        [Test]
        public void Display_NamesTheVisitAndIstDate_NothingClinical()
        {
            // 20:00 UTC on 2 Oct is 01:30 IST on 3 Oct.
            Assert.That(AbdmCareContextRules.Display("OPD", new DateTime(2026, 10, 2, 20, 0, 0, DateTimeKind.Utc)), Is.EqualTo("OPD visit on 03 Oct 2026"));
            Assert.That(AbdmCareContextRules.Display("IPD", new DateTime(2026, 10, 3, 5, 0, 0, DateTimeKind.Utc)), Is.EqualTo("Hospital admission from 03 Oct 2026"));
        }

        [Test]
        public void HiTypes_ListOnlyWhatExists_EmptyWhenNothing()
        {
            Assert.That(AbdmCareContextRules.HiTypes(true, true, true, false), Is.EqualTo(new[] { "Prescription", "DiagnosticReport", "WellnessRecord" }));
            Assert.That(AbdmCareContextRules.HiTypes(false, false, false, false), Is.Empty);
        }

        [TestCase("Female", "F")]
        [TestCase("m", "M")]
        [TestCase("Other", "O")]
        [TestCase("", null)]
        [TestCase(null, null)]
        public void Gender_IsNeverGuessed(string? value, string? expected) =>
            Assert.That(AbdmCareContextRules.GenderCode(value), Is.EqualTo(expected));

        [TestCase("15-08-1990", 1990)]
        [TestCase("1990-08-15", 1990)]
        [TestCase("1990", 1990)]
        public void YearOfBirth_FromAbdmDate(string dob, int year) =>
            Assert.That(AbdmCareContextRules.YearOfBirth(dob, null), Is.EqualTo(year));

        [Test]
        public void JwtExpiry_ReadsTheExpClaim_AndNullForNonJwt()
        {
            var expires = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            Assert.That(AbdmCareContextRules.JwtExpiry(Jwt(expires)), Is.EqualTo(expires));
            Assert.That(AbdmCareContextRules.JwtExpiry("not-a-jwt"), Is.Null);
        }

        [Test]
        public void FindString_SearchesNestedBodies()
        {
            Assert.That(AbdmCareContextRules.FindString("{\"data\":[{\"linkToken\":\"t1\"}]}", "linkToken"), Is.EqualTo("t1"));
            Assert.That(AbdmCareContextRules.FindString("{\"x\":1}", "linkToken"), Is.Null);
        }

        // ---------------------------------------------------------------- refusals before sending

        [Test]
        public void PatientWithoutAbha_IsRefused_NothingSent()
        {
            _db.PatientRegistrations.Single().AbhaId = null;
            _db.SaveChanges();

            var ex = Assert.ThrowsAsync<AbdmLinkingRefusedException>(() => Service().StartAsync(_hospitalId, "OPD", _apptId, "desk", CancellationToken.None));

            Assert.That(ex!.Message, Does.Contain("no ABHA"));
            _hie.Verify(h => h.SendAsync(It.IsAny<AbdmHieCall>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public void VisitWithNoRecords_IsRefused_NothingSent()
        {
            _db.Prescription.RemoveRange(_db.Prescription);
            _db.SaveChanges();

            var ex = Assert.ThrowsAsync<AbdmLinkingRefusedException>(() => Service().StartAsync(_hospitalId, "OPD", _apptId, "desk", CancellationToken.None));

            Assert.That(ex!.Message, Does.Contain("Nothing to link"));
            _hie.Verify(h => h.SendAsync(It.IsAny<AbdmHieCall>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public void VisitFromAnotherHospital_IsRefused()
        {
            Assert.ThrowsAsync<AbdmLinkingRefusedException>(() => Service().StartAsync(Guid.NewGuid(), "OPD", _apptId, "desk", CancellationToken.None));
        }

        // ---------------------------------------------------------------- the journey

        [Test]
        public async Task Start_WithNoToken_AsksForOne_WithAbdmsOwnDemographics()
        {
            AcceptEverySend();

            var careContext = await Service().StartAsync(_hospitalId, "OPD", _apptId, "desk", CancellationToken.None);

            var call = _calls.Single();
            var body = JsonSerializer.Serialize(call.Body);
            Assert.That(call.Kind, Is.EqualTo("link-token"));
            Assert.That(body, Does.Contain("\"abhaNumber\":91123456789012"));
            Assert.That(body, Does.Contain("\"gender\":\"F\""));
            Assert.That(body, Does.Contain("\"yearOfBirth\":1990"));
            Assert.That(careContext.LinkStatus, Is.EqualTo(AbdmLinkStatus.TokenRequested));
            Assert.That(careContext.ReferenceNumber, Is.EqualTo($"OPD-{_apptId:N}"));
            Assert.That(careContext.HiTypes, Is.EqualTo("Prescription"));
        }

        [Test]
        public async Task TokenArrives_IsStoredWithItsExpiry_AndTheLinkIsSent()
        {
            AcceptEverySend();
            await Service().StartAsync(_hospitalId, "OPD", _apptId, "desk", CancellationToken.None);
            var token = Jwt(DateTime.UtcNow.AddHours(6));

            await Service().OnLinkTokenAnsweredAsync(Answer("link-token",
                $"{{\"abhaAddress\":\"{AbhaAddress}\",\"linkToken\":\"{token}\",\"response\":{{\"requestId\":\"x\"}}}}"), CancellationToken.None);

            var stored = _db.AbdmLinkTokens.Single();
            var link = _calls.Last();
            Assert.That(stored.ExpiresAt, Is.Not.Null);
            Assert.That(stored.AbhaAddress, Is.EqualTo(AbhaAddress));
            Assert.That(link.Kind, Is.EqualTo("link-carecontext"));
            Assert.That(link.LinkToken, Is.EqualTo(token));
            Assert.That(JsonSerializer.Serialize(link.Body), Does.Contain($"OPD-{_apptId:N}"));
            Assert.That(Row().LinkStatus, Is.EqualTo(AbdmLinkStatus.LinkRequested));
        }

        [Test]
        public async Task LinkAnswered_MarksTheVisitLinked()
        {
            AcceptEverySend();
            _db.AbdmLinkTokens.Add(new AbdmLinkToken { LinkTokenId = Guid.NewGuid(), HospitalId = _hospitalId, HipId = HipId, AbhaAddress = AbhaAddress, Token = Jwt(DateTime.UtcNow.AddHours(6)), ReceivedAt = DateTime.UtcNow });
            _db.SaveChanges();
            await Service().StartAsync(_hospitalId, "OPD", _apptId, "desk", CancellationToken.None);

            await Service().OnLinkAnsweredAsync(Answer("link-carecontext", "{\"response\":{\"requestId\":\"x\"}}"), CancellationToken.None);

            Assert.That(Row().LinkStatus, Is.EqualTo(AbdmLinkStatus.Linked));
            Assert.That(Row().LinkedAt, Is.Not.Null);
        }

        [Test]
        public async Task UsableStoredToken_SkipsTheTokenRequest()
        {
            AcceptEverySend();
            _db.AbdmLinkTokens.Add(new AbdmLinkToken { LinkTokenId = Guid.NewGuid(), HospitalId = _hospitalId, HipId = HipId, AbhaAddress = AbhaAddress, Token = Jwt(DateTime.UtcNow.AddHours(6)), ReceivedAt = DateTime.UtcNow });
            _db.SaveChanges();

            await Service().StartAsync(_hospitalId, "OPD", _apptId, "desk", CancellationToken.None);

            Assert.That(_calls.Select(c => c.Kind), Is.EqualTo(new[] { "link-carecontext" }));
        }

        [Test]
        public async Task ExpiredStoredToken_IsNotUsed()
        {
            AcceptEverySend();
            _db.AbdmLinkTokens.Add(new AbdmLinkToken { LinkTokenId = Guid.NewGuid(), HospitalId = _hospitalId, HipId = HipId, AbhaAddress = AbhaAddress, Token = "old", ExpiresAt = DateTime.UtcNow.AddMinutes(-5), ReceivedAt = DateTime.UtcNow });
            _db.SaveChanges();

            await Service().StartAsync(_hospitalId, "OPD", _apptId, "desk", CancellationToken.None);

            Assert.That(_calls.Single().Kind, Is.EqualTo("link-token"));
        }

        [Test]
        public async Task TokenFromACounterQrShare_IsUsed()
        {
            AcceptEverySend();
            var shared = Jwt(DateTime.UtcNow.AddHours(6));
            _db.AbdmProfileShares.Add(new AbdmProfileShare { ProfileShareId = Guid.NewGuid(), HospitalId = _hospitalId, HipId = HipId, RequestId = "s", AbhaAddress = AbhaAddress, LinkToken = shared, ReceivedAt = DateTime.UtcNow });
            _db.SaveChanges();

            await Service().StartAsync(_hospitalId, "OPD", _apptId, "desk", CancellationToken.None);

            Assert.That(_calls.Single().Kind, Is.EqualTo("link-carecontext"));
            Assert.That(_calls.Single().LinkToken, Is.EqualTo(shared));
        }

        [Test]
        public async Task TokenRefusedOnTheCallback_FailsTheVisit_WithAbdmsCode()
        {
            AcceptEverySend();
            await Service().StartAsync(_hospitalId, "OPD", _apptId, "desk", CancellationToken.None);
            var exchange = _db.AbdmExchanges.Single();
            exchange.ErrorCode = "ABDM-1092";
            exchange.ErrorMessage = "Duplicate link token request";
            _db.SaveChanges();

            await Service().OnLinkTokenAnsweredAsync(Answer("link-token", "{\"error\":{\"code\":\"ABDM-1092\"},\"response\":{\"requestId\":\"x\"}}"), CancellationToken.None);

            Assert.That(Row().LinkStatus, Is.EqualTo(AbdmLinkStatus.Failed));
            Assert.That(Row().LastErrorCode, Is.EqualTo("ABDM-1092"));
            Assert.That(_calls, Has.Count.EqualTo(1), "no link is sent on a refused token");
        }

        [Test]
        public async Task SynchronousRefusal_FailsTheVisit_WithoutThrowing()
        {
            _hie.Setup(h => h.SendAsync(It.IsAny<AbdmHieCall>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new AbdmException("User not found (ABDM-9999). Reference: r", "ABDM-9999", 400, "r"));

            var careContext = await Service().StartAsync(_hospitalId, "OPD", _apptId, "desk", CancellationToken.None);

            Assert.That(careContext.LinkStatus, Is.EqualTo(AbdmLinkStatus.Failed));
            Assert.That(careContext.LastErrorCode, Is.EqualTo("ABDM-9999"));
        }

        [Test]
        public async Task StartingAgainWhileInFlight_SendsNothingMore()
        {
            AcceptEverySend();
            await Service().StartAsync(_hospitalId, "OPD", _apptId, "desk", CancellationToken.None);

            await Service().StartAsync(_hospitalId, "OPD", _apptId, "desk", CancellationToken.None);

            Assert.That(_calls, Has.Count.EqualTo(1), "a second token request would only come back ABDM-1092");
        }

        [Test]
        public async Task FailedVisit_CanBeStartedAgain()
        {
            _hie.SetupSequence(h => h.SendAsync(It.IsAny<AbdmHieCall>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new AbdmException("x", "ABDM-9999", 400, "r"));
            await Service().StartAsync(_hospitalId, "OPD", _apptId, "desk", CancellationToken.None);
            AcceptEverySend();

            var careContext = await Service().StartAsync(_hospitalId, "OPD", _apptId, "desk", CancellationToken.None);

            Assert.That(careContext.LinkStatus, Is.EqualTo(AbdmLinkStatus.TokenRequested));
            Assert.That(careContext.LastErrorCode, Is.Null);
        }
    }
}
