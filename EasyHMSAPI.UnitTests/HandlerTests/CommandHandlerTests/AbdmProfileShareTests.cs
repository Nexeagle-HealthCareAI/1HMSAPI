using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class AbdmProfileShareParserTests
    {
        private static ParsedProfileShare Parse(string json)
        {
            using var doc = JsonDocument.Parse(json);
            return AbdmProfileShareParser.Parse(doc.RootElement);
        }

        [Test]
        public void Parse_NestedLegacyShape_ReadsPatientProfileAndFacility()
        {
            var p = Parse(@"{
                ""requestId"": ""req-1"",
                ""profile"": {
                    ""hipCode"": ""IN3410000260"",
                    ""counterId"": ""12345"",
                    ""patient"": {
                        ""healthIdNumber"": ""91-1234-5678-9012"",
                        ""healthId"": ""asha@sbx"",
                        ""name"": ""Asha Verma"",
                        ""gender"": ""F"",
                        ""yearOfBirth"": 1990, ""monthOfBirth"": 3, ""dayOfBirth"": 7,
                        ""address"": { ""line"": ""12 MG Road"", ""district"": ""Pune"", ""state"": ""MH"", ""pincode"": ""411001"" },
                        ""identifiers"": [ { ""type"": ""MOBILE"", ""value"": ""9876543210"" } ]
                    }
                },
                ""linkToken"": ""tok-abc""
            }");

            Assert.That(p.RequestId, Is.EqualTo("req-1"));
            Assert.That(p.HipId, Is.EqualTo("IN3410000260"));
            Assert.That(p.CounterId, Is.EqualTo("12345"));
            Assert.That(p.AbhaNumber, Is.EqualTo("91-1234-5678-9012"));
            Assert.That(p.AbhaAddress, Is.EqualTo("asha@sbx"));
            Assert.That(p.FullName, Is.EqualTo("Asha Verma"));
            Assert.That(p.Gender, Is.EqualTo("F"));
            Assert.That(p.DateOfBirth, Is.EqualTo("07-03-1990"));
            Assert.That(p.Mobile, Is.EqualTo("9876543210"));
            Assert.That(p.Address, Is.EqualTo("12 MG Road, Pune, MH, 411001"));
            Assert.That(p.LinkToken, Is.EqualTo("tok-abc"));
        }

        [Test]
        public void Parse_FlatV3Shape_ReadsAbhaFields()
        {
            var p = Parse(@"{ ""hipId"": ""HIP-1"", ""abhaNumber"": ""91123456789012"", ""abhaAddress"": ""r@abdm"",
                             ""name"": ""Ravi"", ""dob"": ""1985-01-02"", ""mobile"": ""9000000001"", ""address"": ""Delhi"" }");

            Assert.That(p.HipId, Is.EqualTo("HIP-1"));
            Assert.That(p.AbhaNumber, Is.EqualTo("91123456789012"));
            Assert.That(p.AbhaAddress, Is.EqualTo("r@abdm"));
            Assert.That(p.FullName, Is.EqualTo("Ravi"));
            Assert.That(p.DateOfBirth, Is.EqualTo("1985-01-02"));
            Assert.That(p.Mobile, Is.EqualTo("9000000001"));
            Assert.That(p.Address, Is.EqualTo("Delhi"));
        }

        [Test]
        public void Parse_TopLevelFacilityName_IsNotMistakenForPatientName()
        {
            var p = Parse(@"{ ""name"": ""City Hospital"", ""patient"": { ""name"": ""Real Patient"" }, ""hipId"": ""H"" }");
            Assert.That(p.FullName, Is.EqualTo("Real Patient"));
        }

        [Test]
        public void Parse_EmptyObject_ReturnsAllNulls()
        {
            var p = Parse("{}");
            Assert.That(p.HipId, Is.Null);
            Assert.That(p.AbhaNumber, Is.Null);
            Assert.That(p.FullName, Is.Null);
        }
    }

    [TestFixture]
    public class AbdmProfileShareHandlerTests
    {
        private AppDbContext _context = null!;
        private Guid _hospitalId;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _hospitalId = Guid.NewGuid();
            _context.AbdmFacilities.Add(new AbdmFacility { HospitalId = _hospitalId, HipId = "IN3410000260", UpdatedAt = DateTime.UtcNow });
            _context.SaveChanges();
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private RecordAbdmProfileShareHandler RecordHandler() =>
            new(_context, NullLogger<RecordAbdmProfileShareHandler>.Instance);

        private const string Body = @"{ ""hipId"": ""IN3410000260"", ""counterId"": ""C1"", ""abhaNumber"": ""91-1234-5678-9012"", ""name"": ""Asha Verma"" }";

        [Test]
        public async Task Record_KnownHip_StoresShareForThatHospital()
        {
            var result = await RecordHandler().Handle(new RecordAbdmProfileShareRequestModel { RawBody = Body, HeaderRequestId = "REQ-1" }, CancellationToken.None);

            Assert.That(result.Accepted, Is.True);
            Assert.That(result.Duplicate, Is.False);
            Assert.That(result.CallbackRequestId, Is.EqualTo("REQ-1"));
            var stored = _context.AbdmProfileShares.Single();
            Assert.That(stored.HospitalId, Is.EqualTo(_hospitalId));
            Assert.That(stored.CounterId, Is.EqualTo("C1"));
            Assert.That(stored.StatusCode, Is.EqualTo("NEW"));
            Assert.That(stored.RawPayload, Is.EqualTo(Body));
        }

        [Test]
        public async Task Record_HipIdMatchIsCaseInsensitive()
        {
            var body = Body.Replace("IN3410000260", "in3410000260");
            var result = await RecordHandler().Handle(new RecordAbdmProfileShareRequestModel { RawBody = body, HeaderRequestId = "REQ-1" }, CancellationToken.None);
            Assert.That(result.Accepted, Is.True);
        }

        [Test]
        public async Task Record_SameRequestIdTwice_IsIdempotent()
        {
            var handler = RecordHandler();
            await handler.Handle(new RecordAbdmProfileShareRequestModel { RawBody = Body, HeaderRequestId = "REQ-1" }, CancellationToken.None);
            var second = await handler.Handle(new RecordAbdmProfileShareRequestModel { RawBody = Body, HeaderRequestId = "REQ-1" }, CancellationToken.None);

            Assert.That(second.Accepted, Is.True);
            Assert.That(second.Duplicate, Is.True);
            Assert.That(_context.AbdmProfileShares.Count(), Is.EqualTo(1));
        }

        [Test]
        public async Task Record_UnknownHip_IsNotStored()
        {
            var body = Body.Replace("IN3410000260", "SOMEONE-ELSE");
            var result = await RecordHandler().Handle(new RecordAbdmProfileShareRequestModel { RawBody = body, HeaderRequestId = "REQ-1" }, CancellationToken.None);

            Assert.That(result.Accepted, Is.False);
            Assert.That(_context.AbdmProfileShares.Any(), Is.False);
        }

        [Test]
        public async Task Record_InvalidJson_IsRejectedWithoutThrowing()
        {
            var result = await RecordHandler().Handle(new RecordAbdmProfileShareRequestModel { RawBody = "not json" }, CancellationToken.None);
            Assert.That(result.Accepted, Is.False);
        }

        [Test]
        public async Task Get_MarksReturningPatient_EvenWhenAbhaFormatDiffers()
        {
            _context.PatientRegistrations.Add(NewPatient("PTID00000001", "Asha V", "91123456789012"));
            await _context.SaveChangesAsync();
            await RecordHandler().Handle(new RecordAbdmProfileShareRequestModel { RawBody = Body, HeaderRequestId = "REQ-1" }, CancellationToken.None);

            var result = await new GetAbdmProfileSharesHandler(_context)
                .Handle(new GetAbdmProfileSharesRequestModel { HospitalId = _hospitalId }, CancellationToken.None);

            var item = result.Items.Single();
            Assert.That(item.ExistingPatientId, Is.EqualTo("PTID00000001"));
        }

        [Test]
        public async Task Get_NewPatient_HasNoExistingPatient_AndMergedPatientsAreIgnored()
        {
            var merged = NewPatient("PTID00000002", "Asha Old", "91123456789012");
            merged.MergedIntoPatientId = "PTID00000009";
            _context.PatientRegistrations.Add(merged);
            await _context.SaveChangesAsync();
            await RecordHandler().Handle(new RecordAbdmProfileShareRequestModel { RawBody = Body, HeaderRequestId = "REQ-1" }, CancellationToken.None);

            var result = await new GetAbdmProfileSharesHandler(_context)
                .Handle(new GetAbdmProfileSharesRequestModel { HospitalId = _hospitalId }, CancellationToken.None);

            Assert.That(result.Items.Single().ExistingPatientId, Is.Null);
        }

        [Test]
        public async Task Handle_MarksHandled_AndKeepsAbhaAccountOnRecord()
        {
            await RecordHandler().Handle(new RecordAbdmProfileShareRequestModel { RawBody = Body, HeaderRequestId = "REQ-1" }, CancellationToken.None);
            var shareId = _context.AbdmProfileShares.Single().ProfileShareId;

            var result = await new HandleAbdmProfileShareHandler(_context).Handle(
                new HandleAbdmProfileShareRequestModel { HospitalId = _hospitalId, ProfileShareId = shareId, LoggedInUserName = "Reception" },
                CancellationToken.None);

            Assert.That(result.Success, Is.True);
            Assert.That(_context.AbdmProfileShares.Single().StatusCode, Is.EqualTo("HANDLED"));
            var account = _context.AbhaAccount.Single();
            Assert.That(account.Source, Is.EqualTo("FacilityQR"));
            Assert.That(account.AbhaNumber, Is.EqualTo("91-1234-5678-9012"));
        }

        [Test]
        public async Task CheckDuplicates_AbhaMatch_RanksFirstEvenWithDissimilarName()
        {
            _context.PatientRegistrations.Add(NewPatient("PTID00000003", "Completely Different Name", "91-1234-5678-9012"));
            _context.PatientRegistrations.Add(NewPatient("PTID00000004", "Asha Verma", null, mobile: "9876543210"));
            await _context.SaveChangesAsync();

            var result = await new CheckPatientDuplicatesHandler(_context).Handle(new CheckPatientDuplicatesRequestModel
            {
                HospitalId = _hospitalId,
                FullName = "Asha Verma",
                Mobile = "9876543210",
                AbhaId = "91-1234-5678-9012"
            }, CancellationToken.None);

            Assert.That(result.Matches.First().PatientId, Is.EqualTo("PTID00000003"));
            Assert.That(result.Matches.First().Confidence, Is.EqualTo("ABHA_VERIFIED"));
            Assert.That(result.Matches.First().MatchedOn, Does.Contain("ABHA"));
        }

        private PatientRegistration NewPatient(string patientId, string name, string? abhaId, string mobile = "9000000000") => new()
        {
            RegistrationId = Guid.NewGuid(),
            HospitalId = _hospitalId,
            PatientId = patientId,
            RegisteredAt = DateTime.UtcNow,
            FullName = name,
            Mobile = mobile,
            Age = 30,
            AgeUnit = "Y",
            Country = "India",
            AbhaId = abhaId
        };
    }
}
