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
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    // ABDM M2 phase 4: user-initiated linking. discover → on-discover, init → on-init (OTP),
    // confirm → on-confirm. Every question gets an answer that quotes its REQUEST-ID, and a
    // patient can only ever be shown, and link, their own visits.
    [TestFixture]
    public class AbdmUserLinkingTests
    {
        private AppDbContext _db = null!;
        private Mock<IAbdmHieService> _hie = null!;
        private Mock<IWhatsAppMessagingService> _whatsApp = null!;
        private List<AbdmHieCall> _calls = null!;
        private string? _otp, _otpMobile;
        private Guid _hospitalId, _apptId;
        private const string PatientId = "P-0001";
        private const string AbhaAddress = "asha@sbx";

        [SetUp]
        public void SetUp()
        {
            _db = InMemoryDbContextFactory.CreateContext();
            _hospitalId = Guid.NewGuid();
            _apptId = Guid.NewGuid();
            _db.AbdmFacilities.Add(new AbdmFacility { HospitalId = _hospitalId, HipId = "IN2810014366", UpdatedAt = DateTime.UtcNow });
            _db.Appointments.Add(new Appointment { ApptId = _apptId, HospitalId = _hospitalId, PatientId = PatientId, ApptDate = new DateTime(2026, 10, 3, 5, 0, 0, DateTimeKind.Utc) });
            _db.PatientRegistrations.Add(new PatientRegistration
            {
                RegistrationId = Guid.NewGuid(), HospitalId = _hospitalId, PatientId = PatientId, FullName = "Asha Rao", Sex = "Female",
                Mobile = "9876543210", DateOfBirth = new DateTime(1990, 3, 5), AbhaId = "91-1234-5678-9012", RegisteredAt = new DateTime(2026, 1, 1)
            });
            _db.Prescription.Add(new Prescription { PrescriptionId = Guid.NewGuid(), ApptId = _apptId, HospitalId = _hospitalId, PatientId = PatientId, CreatedAt = DateTime.UtcNow });
            _db.SaveChanges();

            _calls = new List<AbdmHieCall>();
            _hie = new Mock<IAbdmHieService>();
            _hie.Setup(h => h.SendAsync(It.IsAny<AbdmHieCall>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((AbdmHieCall call, CancellationToken _) =>
                {
                    _calls.Add(call);
                    return new AbdmHieResult { ExchangeId = Guid.NewGuid(), RequestId = Guid.NewGuid().ToString(), HttpStatus = 202 };
                });

            _otp = null;
            _otpMobile = null;
            _whatsApp = new Mock<IWhatsAppMessagingService>();
            _whatsApp.Setup(w => w.SendOtpAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string>((mobile, otp) => { _otpMobile = mobile; _otp = otp; })
                .ReturnsAsync(true);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_db);
            _db.Dispose();
        }

        private AbdmUserLinkingService Service() => new(
            _db, _hie.Object,
            new AbdmLinkingService(_db, _hie.Object, NullLogger<AbdmLinkingService>.Instance),
            _whatsApp.Object, NullLogger<AbdmUserLinkingService>.Instance);

        private AbdmInbound Inbound(string requestId, string body) => new()
        {
            InboundId = Guid.NewGuid(), HospitalId = _hospitalId, HeaderRequestId = requestId, RawBody = body,
            Verdict = AbdmInboundVerdict.Accepted, ReceivedAt = DateTime.UtcNow
        };

        private static string DiscoverBody(string name = "Asha Rao", string gender = "F", int year = 1990, string mobile = "+919876543210", string? abhaNumber = "91-1234-5678-9012", string? mr = null)
        {
            var verified = new List<object> { new { type = "MOBILE", value = mobile } };
            if (abhaNumber != null)
                verified.Add(new { type = "ABHA_NUMBER", value = abhaNumber });
            var unverified = new List<object>();
            if (mr != null)
                unverified.Add(new { type = "MR", value = mr });
            return JsonSerializer.Serialize(new
            {
                transactionId = "txn-1",
                patient = new { id = AbhaAddress, name, gender, yearOfBirth = year, verifiedIdentifiers = verified, unverifiedIdentifiers = unverified }
            });
        }

        private string Reference => AbdmCareContextRules.ReferenceNumber("OPD", _apptId);

        private string InitBody(string? reference = null) => JsonSerializer.Serialize(new
        {
            transactionId = "txn-1",
            abhaAddress = AbhaAddress,
            patient = new[] { new { referenceNumber = PatientId, display = "Asha Rao", careContexts = new[] { new { referenceNumber = reference ?? Reference } }, hiType = "Prescription", count = 1 } }
        });

        // ABDM documents confirmation.token as an integer, so that is what the tests send.
        private static string ConfirmBody(string linkReference, string otp) =>
            JsonSerializer.Serialize(new { confirmation = new { token = int.Parse(otp), linkRefNumber = linkReference } });

        private static JsonElement Body(AbdmHieCall call) => JsonDocument.Parse(JsonSerializer.Serialize(call.Body)).RootElement;

        private static AbdmDiscoveryCandidate Candidate(string id, string name, string mobile = "9876543210", string sex = "Female", int year = 1990, string? abha = null) =>
            new(id, name, mobile, sex, year, true, abha, new DateTime(2026, 1, 1));

        // ---------------------------------------------------------------- rules

        [Test]
        public void Parse_ReadsThePatientAndBothKindsOfIdentifier()
        {
            var request = AbdmDiscoveryRules.Parse(DiscoverBody(mr: "P-0001"));

            Assert.That(request.TransactionId, Is.EqualTo("txn-1"));
            Assert.That(request.AbhaAddress, Is.EqualTo(AbhaAddress));
            Assert.That(request.YearOfBirth, Is.EqualTo(1990));
            Assert.That(request.VerifiedValue("mobile"), Is.EqualTo("+919876543210"));
            Assert.That(request.VerifiedValue("ABHA_NUMBER"), Is.EqualTo("91-1234-5678-9012"));
            Assert.That(request.UnverifiedValue("MR"), Is.EqualTo("P-0001"));
            Assert.That(AbdmDiscoveryRules.Parse("not json").TransactionId, Is.Null);
        }

        [Test]
        public void Match_OnAbhaNumber_NeedsNothingElse()
        {
            var request = AbdmDiscoveryRules.Parse(DiscoverBody(name: "Completely Different", mobile: "+910000000000"));

            var match = AbdmDiscoveryRules.Match(request, new[] { Candidate("P-1", "Asha Rao", abha: "91123456789012") });

            Assert.That(match!.Patient.PatientId, Is.EqualTo("P-1"));
            Assert.That(match.MatchedBy, Is.EqualTo(new[] { "ABHA_NUMBER" }));
        }

        [Test]
        public void Match_OnMobile_AlsoNeedsNameGenderAndYearToAgree()
        {
            var request = AbdmDiscoveryRules.Parse(DiscoverBody(abhaNumber: null));
            var asha = Candidate("P-1", "Asha Rao");

            Assert.That(AbdmDiscoveryRules.Match(request, new[] { asha })!.MatchedBy, Is.EqualTo(new[] { "MOBILE" }));
            // Same phone, someone else in the family.
            Assert.That(AbdmDiscoveryRules.Match(request, new[] { Candidate("P-2", "Vikram Shah", sex: "Male") }), Is.Null);
            Assert.That(AbdmDiscoveryRules.Match(request, new[] { Candidate("P-3", "Asha Rao", sex: "Male") }), Is.Null);
            Assert.That(AbdmDiscoveryRules.Match(request, new[] { Candidate("P-4", "Asha Rao", year: 1960) }), Is.Null);
            Assert.That(AbdmDiscoveryRules.Match(request, new[] { Candidate("P-5", "Meera Iyer") }), Is.Null);
        }

        [Test]
        public void Match_OnMobile_AllowsYearOfBirthWithinFiveYears_Usr606()
        {
            var request = AbdmDiscoveryRules.Parse(DiscoverBody(abhaNumber: null));

            Assert.That(AbdmDiscoveryRules.Match(request, new[] { Candidate("P-1", "Asha Rao", year: 1995) }), Is.Not.Null);
            Assert.That(AbdmDiscoveryRules.Match(request, new[] { Candidate("P-2", "Asha Rao", year: 1985) }), Is.Not.Null);
            Assert.That(AbdmDiscoveryRules.Match(request, new[] { Candidate("P-3", "Asha Rao", year: 1996) }), Is.Null);
            Assert.That(AbdmDiscoveryRules.Match(request, new[] { Candidate("P-4", "Asha Rao", year: 1984) }), Is.Null);
        }

        [Test]
        public void Match_ToTwoPatients_IsNoMatch_UnlessTheTypedPatientIdChooses()
        {
            var two = new[] { Candidate("P-1", "Asha Rao"), Candidate("P-2", "Asha Rao") };

            Assert.That(AbdmDiscoveryRules.Match(AbdmDiscoveryRules.Parse(DiscoverBody(abhaNumber: null)), two), Is.Null);

            var chosen = AbdmDiscoveryRules.Match(AbdmDiscoveryRules.Parse(DiscoverBody(abhaNumber: null, mr: "P-2")), two);
            Assert.That(chosen!.Patient.PatientId, Is.EqualTo("P-2"));
            Assert.That(chosen.MatchedBy, Is.EqualTo(new[] { "MOBILE", "MR" }));
        }

        [Test]
        public void Match_OnATypedPatientIdAlone_IsNoMatch()
        {
            var body = JsonSerializer.Serialize(new { transactionId = "t", patient = new { id = "someone@sbx", name = "Asha Rao", unverifiedIdentifiers = new[] { new { type = "MR", value = "P-1" } } } });

            Assert.That(AbdmDiscoveryRules.Match(AbdmDiscoveryRules.Parse(body), new[] { Candidate("P-1", "Asha Rao") }), Is.Null);
        }

        [Test]
        public void RequestedReferences_AreOnlyTheOnesUnderCareContexts()
        {
            var body = "{\"patient\":[{\"referenceNumber\":\"P-0001\",\"careContexts\":[{\"referenceNumber\":\"OPD-a\"},{\"referenceNumber\":\"OPD-b\"}]}]}";

            Assert.That(AbdmDiscoveryRules.RequestedReferences(body), Is.EqualTo(new[] { "OPD-a", "OPD-b" }));
            Assert.That(AbdmDiscoveryRules.RequestedReferences("nope"), Is.Empty);
        }

        [Test]
        public void Otp_IsCheckedAgainstItsOwnLinkReferenceOnly()
        {
            var hash = AbdmDiscoveryRules.OtpHash("123456", "ref-1");

            Assert.That(AbdmDiscoveryRules.OtpMatches("123456", "ref-1", hash), Is.True);
            Assert.That(AbdmDiscoveryRules.OtpMatches(" 123456 ", "ref-1", hash), Is.True);
            Assert.That(AbdmDiscoveryRules.OtpMatches("123457", "ref-1", hash), Is.False);
            Assert.That(AbdmDiscoveryRules.OtpMatches("123456", "ref-2", hash), Is.False);
            Assert.That(AbdmDiscoveryRules.OtpMatches(null, "ref-1", hash), Is.False);
            Assert.That(AbdmDiscoveryRules.NewOtp(), Has.Length.EqualTo(6));
            Assert.That(AbdmDiscoveryRules.NewOtp(), Does.Not.StartWith("0"));
        }

        [Test]
        public void TheConfirmOtp_IsReadAsANumberOrAsText()
        {
            Assert.That(AbdmDiscoveryRules.ConfirmToken("{\"confirmation\":{\"token\":123456,\"linkRefNumber\":\"r\"}}"), Is.EqualTo("123456"));
            Assert.That(AbdmDiscoveryRules.ConfirmToken("{\"confirmation\":{\"token\":\"123456\",\"linkRefNumber\":\"r\"}}"), Is.EqualTo("123456"));
            Assert.That(AbdmDiscoveryRules.ConfirmToken("{\"confirmation\":{\"token\":12345,\"linkRefNumber\":\"r\"}}"), Is.EqualTo("012345"));
            Assert.That(AbdmDiscoveryRules.ConfirmToken("{\"confirmation\":{\"linkRefNumber\":\"r\"}}"), Is.Null);
        }

        // ---------------------------------------------------------------- discover

        [Test]
        public async Task Discover_OffersTheUnlinkedVisit_AndQuotesTheRequestId()
        {
            await Service().OnDiscoverAsync(Inbound("req-discover", DiscoverBody()), CancellationToken.None);

            var call = _calls.Single();
            Assert.That(call.Path, Does.EndWith("patient/care-context/on-discover"));
            Assert.That(call.ExpectsCallback, Is.False);
            var body = Body(call);
            Assert.That(body.GetProperty("transactionId").GetString(), Is.EqualTo("txn-1"));
            Assert.That(body.GetProperty("response").GetProperty("requestId").GetString(), Is.EqualTo("req-discover"));
            Assert.That(body.GetProperty("matchedBy")[0].GetString(), Is.EqualTo("ABHA_NUMBER"));
            var entry = body.GetProperty("patient")[0];
            Assert.That(entry.GetProperty("referenceNumber").GetString(), Is.EqualTo(PatientId));
            Assert.That(entry.GetProperty("hiType").GetString(), Is.EqualTo("Prescription"));
            Assert.That(entry.GetProperty("careContexts")[0].GetProperty("referenceNumber").GetString(), Is.EqualTo(Reference));
            Assert.That(entry.GetProperty("careContexts")[0].GetProperty("display").GetString(), Is.EqualTo("OPD visit on 03 Oct 2026"));

            var stored = _db.AbdmLinkRequests.Single();
            Assert.That(stored.PatientId, Is.EqualTo(PatientId));
            Assert.That(stored.OfferedReferences, Is.EqualTo(Reference));
            Assert.That(_db.AbdmCareContexts.Single().LinkStatus, Is.EqualTo(AbdmLinkStatus.NotLinked));
        }

        [Test]
        public async Task Discover_ForSomeoneWeDoNotKnow_SaysSo_AndStoresNothing()
        {
            await Service().OnDiscoverAsync(Inbound("req-x", DiscoverBody(name: "Nobody Here", mobile: "+910000000000", abhaNumber: "91-0000-0000-0000")), CancellationToken.None);

            var body = Body(_calls.Single());
            Assert.That(body.GetProperty("error").GetProperty("code").GetString(), Is.EqualTo(AbdmUserLinkingService.NoPatientCode));
            Assert.That(body.GetProperty("response").GetProperty("requestId").GetString(), Is.EqualTo("req-x"));
            Assert.That(body.TryGetProperty("patient", out _), Is.False);
            Assert.That(_db.AbdmLinkRequests.Any(), Is.False);
        }

        [Test]
        public async Task Discover_DoesNotOfferAVisitThatIsAlreadyLinked()
        {
            _db.AbdmCareContexts.Add(new AbdmCareContext
            {
                CareContextId = Guid.NewGuid(), HospitalId = _hospitalId, PatientId = PatientId, SourceType = "OPD", SourceId = _apptId,
                ReferenceNumber = Reference, Display = "OPD visit on 03 Oct 2026", HiTypes = "Prescription", LinkStatus = AbdmLinkStatus.Linked,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });
            _db.SaveChanges();

            await Service().OnDiscoverAsync(Inbound("req-1", DiscoverBody()), CancellationToken.None);

            Assert.That(Body(_calls.Single()).GetProperty("error").GetProperty("code").GetString(), Is.EqualTo(AbdmUserLinkingService.NoRecordsCode));
        }

        [Test]
        public async Task Discover_NeverShowsAnotherHospitalsPatient()
        {
            var inbound = Inbound("req-1", DiscoverBody());
            inbound.HospitalId = Guid.NewGuid();
            _db.AbdmFacilities.Add(new AbdmFacility { HospitalId = inbound.HospitalId.Value, HipId = "IN0000000001", UpdatedAt = DateTime.UtcNow });
            _db.SaveChanges();

            await Service().OnDiscoverAsync(inbound, CancellationToken.None);

            Assert.That(Body(_calls.Single()).GetProperty("error").GetProperty("code").GetString(), Is.EqualTo(AbdmUserLinkingService.NoPatientCode));
        }

        // ---------------------------------------------------------------- init and confirm

        private async Task<string> DiscoverAndInitAsync()
        {
            await Service().OnDiscoverAsync(Inbound("req-discover", DiscoverBody()), CancellationToken.None);
            await Service().OnInitAsync(Inbound("req-init", InitBody()), CancellationToken.None);
            return _db.AbdmLinkRequests.Single().LinkReferenceNumber!;
        }

        [Test]
        public async Task Init_SendsAnOtpToTheMobileOnOurRecord_AndNeverStoresIt()
        {
            var linkReference = await DiscoverAndInitAsync();

            Assert.That(_otpMobile, Is.EqualTo("9876543210"));
            Assert.That(_otp, Has.Length.EqualTo(6));
            var stored = _db.AbdmLinkRequests.Single();
            Assert.That(stored.Status, Is.EqualTo(AbdmLinkRequestStatus.OtpSent));
            Assert.That(stored.OtpHash, Has.Length.EqualTo(64));
            Assert.That(stored.OtpHash, Is.Not.EqualTo(_otp));
            Assert.That(stored.RequestedReferences, Is.EqualTo(Reference));

            var call = _calls.Last();
            Assert.That(call.Path, Does.EndWith("link/care-context/on-init"));
            var body = Body(call);
            Assert.That(body.GetProperty("response").GetProperty("requestId").GetString(), Is.EqualTo("req-init"));
            Assert.That(body.GetProperty("link").GetProperty("referenceNumber").GetString(), Is.EqualTo(linkReference));
            Assert.That(body.GetProperty("link").GetProperty("meta").GetProperty("communicationHint").GetString(), Is.EqualTo("XXXXXX3210"));
            Assert.That(JsonSerializer.Serialize(call.Body), Does.Not.Contain(_otp!));
        }

        [Test]
        public async Task Init_ForARecordThatWasNotOffered_IsRefused_AndNoOtpGoesOut()
        {
            await Service().OnDiscoverAsync(Inbound("req-discover", DiscoverBody()), CancellationToken.None);

            await Service().OnInitAsync(Inbound("req-init", InitBody(reference: "OPD-someone-elses-visit")), CancellationToken.None);

            Assert.That(Body(_calls.Last()).GetProperty("error").GetProperty("code").GetString(), Is.EqualTo(AbdmUserLinkingService.InvalidCareContextsCode));
            _whatsApp.Verify(w => w.SendOtpAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public async Task Init_WithoutADiscovery_IsRefused()
        {
            await Service().OnInitAsync(Inbound("req-init", InitBody()), CancellationToken.None);

            Assert.That(Body(_calls.Single()).GetProperty("error").GetProperty("code").GetString(), Is.EqualTo(AbdmUserLinkingService.InvalidTransactionCode));
            _whatsApp.Verify(w => w.SendOtpAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public async Task Init_WhenTheOtpCannotBeSent_SaysSo()
        {
            _whatsApp.Setup(w => w.SendOtpAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);
            await Service().OnDiscoverAsync(Inbound("req-discover", DiscoverBody()), CancellationToken.None);

            await Service().OnInitAsync(Inbound("req-init", InitBody()), CancellationToken.None);

            Assert.That(Body(_calls.Last()).GetProperty("error").GetProperty("code").GetString(), Is.EqualTo(AbdmUserLinkingService.OtpNotSentCode));
            Assert.That(_db.AbdmLinkRequests.Single().Status, Is.EqualTo(AbdmLinkRequestStatus.Discovered));
        }

        [Test]
        public async Task Confirm_WithTheRightOtp_LinksTheVisit_AndAnswersWithIt()
        {
            var linkReference = await DiscoverAndInitAsync();

            await Service().OnConfirmAsync(Inbound("req-confirm", ConfirmBody(linkReference, _otp!)), CancellationToken.None);

            var careContext = _db.AbdmCareContexts.Single();
            Assert.That(careContext.LinkStatus, Is.EqualTo(AbdmLinkStatus.Linked));
            Assert.That(careContext.LinkedAt, Is.Not.Null);
            Assert.That(careContext.AbhaAddress, Is.EqualTo(AbhaAddress));
            Assert.That(_db.AbdmLinkRequests.Single().Status, Is.EqualTo(AbdmLinkRequestStatus.Linked));
            Assert.That(_db.AbdmLinkRequests.Single().OtpHash, Is.Null);

            var call = _calls.Last();
            Assert.That(call.Path, Does.EndWith("link/care-context/on-confirm"));
            var body = Body(call);
            Assert.That(body.GetProperty("response").GetProperty("requestId").GetString(), Is.EqualTo("req-confirm"));
            Assert.That(body.GetProperty("patient")[0].GetProperty("careContexts")[0].GetProperty("referenceNumber").GetString(), Is.EqualTo(Reference));
        }

        [Test]
        public async Task Confirm_WithAWrongOtp_LinksNothing_AndThreeWrongOnesCloseTheAttempt()
        {
            var linkReference = await DiscoverAndInitAsync();
            var wrong = _otp == "000000" ? "111111" : "000000";

            for (var i = 0; i < AbdmDiscoveryRules.OtpMaxAttempts; i++)
                await Service().OnConfirmAsync(Inbound($"req-wrong-{i}", ConfirmBody(linkReference, wrong)), CancellationToken.None);

            Assert.That(Body(_calls.Last()).GetProperty("error").GetProperty("code").GetString(), Is.EqualTo(AbdmUserLinkingService.OtpFailedCode));
            Assert.That(_db.AbdmCareContexts.Single().LinkStatus, Is.EqualTo(AbdmLinkStatus.NotLinked));
            Assert.That(_db.AbdmLinkRequests.Single().Status, Is.EqualTo(AbdmLinkRequestStatus.Closed));

            // The right OTP is no use once the attempt is closed.
            await Service().OnConfirmAsync(Inbound("req-late", ConfirmBody(linkReference, _otp!)), CancellationToken.None);

            Assert.That(Body(_calls.Last()).GetProperty("error").GetProperty("code").GetString(), Is.EqualTo(AbdmUserLinkingService.InvalidLinkReferenceCode));
            Assert.That(_db.AbdmCareContexts.Single().LinkStatus, Is.EqualTo(AbdmLinkStatus.NotLinked));
        }

        [Test]
        public async Task Confirm_AfterTheOtpExpired_LinksNothing()
        {
            var linkReference = await DiscoverAndInitAsync();
            _db.AbdmLinkRequests.Single().OtpExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            _db.SaveChanges();

            await Service().OnConfirmAsync(Inbound("req-confirm", ConfirmBody(linkReference, _otp!)), CancellationToken.None);

            Assert.That(Body(_calls.Last()).GetProperty("error").GetProperty("code").GetString(), Is.EqualTo(AbdmUserLinkingService.OtpFailedCode));
            Assert.That(_db.AbdmCareContexts.Single().LinkStatus, Is.EqualTo(AbdmLinkStatus.NotLinked));
        }

        [Test]
        public async Task Confirm_WithAnUnknownLinkReference_IsRefused()
        {
            await Service().OnConfirmAsync(Inbound("req-confirm", ConfirmBody("made-up", "123456")), CancellationToken.None);

            Assert.That(Body(_calls.Single()).GetProperty("error").GetProperty("code").GetString(), Is.EqualTo(AbdmUserLinkingService.InvalidLinkReferenceCode));
        }
    }
}
