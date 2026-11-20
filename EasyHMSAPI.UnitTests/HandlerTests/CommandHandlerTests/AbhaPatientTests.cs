#nullable enable
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using Patient = EasyHMSAPI.Application.RequestModels.CommandRequestModels.Patient;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    // ABDM M1: registering a patient from a verified ABHA. VRFY_ABHA_306 (form filled, name, birth
    // date and sex the ABHA's), VRFY_ABHA_308 (already linked: no second patient), CRT_ABHA_116
    // (beneficiary matching) and TAGGING_UNIQUEPATIENTID_UNIQUEABHANUMBER (one ABHA per patient).
    // Every number here is invented.
    [TestFixture]
    public class AbhaPatientTests
    {
        private AppDbContext _context = null!;
        private IAbhaLinkProofStore _proofs = null!;
        private Guid _hospitalId, _staff;
        private static readonly DateTime Today = new(2026, 11, 20);

        private static VerifiedAbhaProfile Asha(string number = "91-0000-0000-0001") =>
            new(number, "asha_1@sbx", "Asha Kumari", "F", "05-03-1990", "9000000001", null, "12 Lake Road", "BIHAR", "PURNIA", "Baisi", "854315");

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _proofs = new AbhaLinkProofStore(new MemoryCache(new MemoryCacheOptions()));
            _hospitalId = Guid.NewGuid();
            _staff = HrAuthSeed.SeedMember(_context, _hospitalId);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private PatientRegistration SeedPatient(string id, string name, string? mobile, string? sex = "Female", short? age = 36, string? abhaId = null)
        {
            var p = new PatientRegistration
            {
                RegistrationId = Guid.NewGuid(), HospitalId = _hospitalId, PatientId = id, FullName = name, Mobile = mobile,
                Sex = sex, Age = age, AgeUnit = "Y", AbhaId = abhaId, RegisteredAt = DateTime.UtcNow.AddDays(-30)
            };
            _context.PatientRegistrations.Add(p);
            _context.SaveChanges();
            return p;
        }

        private Task<Application.ResponseModels.CommandResponseModels.RegisterAbhaPatientResponseModel> Register(string token, string? patientId = null, string? mobile = null, string? address = null) =>
            new RegisterAbhaPatientHandler(_context, _proofs).Handle(new RegisterAbhaPatientRequestModel
            {
                HospitalId = _hospitalId, CallerUserId = _staff, LinkToken = token, PatientId = patientId, Mobile = mobile, AddressLine = address
            }, CancellationToken.None);

        private Task<Application.ResponseModels.CommandResponseModels.MatchAbhaPatientResponseModel> Match(string token) =>
            new MatchAbhaPatientHandler(_context, _proofs).Handle(new MatchAbhaPatientRequestModel
            {
                HospitalId = _hospitalId, CallerUserId = _staff, LinkToken = token
            }, CancellationToken.None);

        // ---------------------------------------------------------------- rules

        [Test]
        public void TheAbhaNumber_IsKeptInOneSpelling() =>
            Assert.That(AbhaPatientRules.FormatAbhaNumber("91000000000001"), Is.EqualTo("91-0000-0000-0001"));

        [TestCase("05-03-1990", 36, "Y")]
        [TestCase("1990-03-05", 36, "Y")]
        [TestCase("1990", 36, "Y")]
        [TestCase("01-09-2026", 2, "M")]
        public void TheAge_ComesFromTheAbhaBirthDate(string dob, int age, string unit)
        {
            var got = AbhaPatientRules.Age(dob, Today);
            Assert.That(got, Is.Not.Null);
            Assert.That(got!.Value.Age, Is.EqualTo(age));
            Assert.That(got.Value.Unit, Is.EqualTo(unit));
        }

        [TestCase("M", "Male")]
        [TestCase("F", "Female")]
        [TestCase("O", "Other")]
        [TestCase("", null)]
        public void TheSex_IsTheAbhaGender(string gender, string? sex) =>
            Assert.That(AbhaPatientRules.Sex(gender), Is.EqualTo(sex));

        // ---------------------------------------------------------------- VRFY_ABHA_306: a new patient from the ABHA

        [Test]
        public async Task ANewPatient_IsRegisteredFromTheAbha_WithItsNameBirthDateAndSex()
        {
            var token = _proofs.Issue(_staff, _hospitalId, Asha());
            var res = await Register(token, address: "Ward 4, Baisi");

            Assert.That(res.Success, Is.True, res.Message);
            Assert.That(res.Created, Is.True);
            var p = await _context.PatientRegistrations.SingleAsync(x => x.PatientId == res.PatientId);
            Assert.That(p.AbhaId, Is.EqualTo("91-0000-0000-0001"));
            Assert.That(p.FullName, Is.EqualTo("Asha Kumari"));
            Assert.That(p.Sex, Is.EqualTo("Female"));
            Assert.That(p.DateOfBirth, Is.EqualTo(new DateTime(1990, 3, 5)));
            Assert.That(p.Mobile, Is.EqualTo("9000000001"), "an empty editable field starts from the ABHA");
            Assert.That(p.AddressLine, Is.EqualTo("Ward 4, Baisi"), "an editable field staff changed is kept");
            Assert.That(p.District, Is.EqualTo("PURNIA"));
            var account = await _context.AbhaAccount.SingleAsync();
            Assert.That(account.LinkedPatientId, Is.EqualTo(res.PatientId));
        }

        [Test]
        public async Task TheLinkProof_IsUsedUp_OnlyWhenTheSaveSucceeds()
        {
            var token = _proofs.Issue(_staff, _hospitalId, Asha());
            var bad = await Register(token, mobile: "12345");
            Assert.That(bad.Success, Is.False);
            Assert.That(_proofs.Peek(token, _staff, _hospitalId), Is.Not.Null, "a refused save keeps the proof");
            var ok = await Register(token);
            Assert.That(ok.Success, Is.True, ok.Message);
            Assert.That(_proofs.Peek(token, _staff, _hospitalId), Is.Null);
        }

        // ---------------------------------------------------------------- VRFY_ABHA_308 and tagging

        [Test]
        public async Task AnAbhaAlreadyOnAPatient_IsShownAsLinked_AndNoSecondPatientIsMade()
        {
            SeedPatient("PTID00000001", "Asha Kumari", "9000000001", abhaId: "91-0000-0000-0001");
            var token = _proofs.Issue(_staff, _hospitalId, Asha("91000000000001"));

            var match = await Match(token);
            Assert.That(match.AlreadyLinked?.PatientId, Is.EqualTo("PTID00000001"));
            Assert.That(match.Message, Is.EqualTo(AbhaPatientRules.AlreadyLinkedMessage));

            var res = await Register(token);
            Assert.That(res.Success, Is.False);
            Assert.That(res.AlreadyLinkedPatientId, Is.EqualTo("PTID00000001"));
            Assert.That(await _context.PatientRegistrations.CountAsync(), Is.EqualTo(1));

            var again = await Register(token, patientId: "PTID00000001");
            Assert.That(again.Success, Is.True, again.Message);
            Assert.That(again.Created, Is.False);
            Assert.That(await _context.PatientRegistrations.CountAsync(), Is.EqualTo(1));
        }

        [Test]
        public async Task APatientHoldingAnotherAbha_CannotTakeASecondOne()
        {
            SeedPatient("PTID00000002", "Asha Kumari", "9000000001", abhaId: "91-0000-0000-0099");
            var token = _proofs.Issue(_staff, _hospitalId, Asha());
            var res = await Register(token, patientId: "PTID00000002");
            Assert.That(res.Success, Is.False);
            Assert.That(res.Message, Does.Contain("only one ABHA"));
            var p = await _context.PatientRegistrations.SingleAsync();
            Assert.That(p.AbhaId, Is.EqualTo("91-0000-0000-0099"));
        }

        // ---------------------------------------------------------------- CRT_ABHA_116: beneficiary matching

        [Test]
        public async Task ExistingPatients_WhoMayBeThisPerson_AreOffered_AndOneCanBeLinked()
        {
            SeedPatient("PTID00000003", "Asha Kumari", "9000000001");            // same mobile and name
            SeedPatient("PTID00000004", "Asha Kumary", "9111111111", age: 35);   // close name, sex, age
            SeedPatient("PTID00000005", "Ravi Kumar", "9000000001", sex: "Male"); // same mobile, other person
            SeedPatient("PTID00000006", "Asha Kumari", "9222222222", age: 70);   // same name, far age
            var token = _proofs.Issue(_staff, _hospitalId, Asha());

            var match = await Match(token);
            var ids = match.Candidates.Select(c => c.PatientId).ToList();
            Assert.That(ids, Is.EquivalentTo(new[] { "PTID00000003", "PTID00000004" }));
            Assert.That(match.Candidates[0].MatchedOn, Does.Contain("MOBILE"));

            var res = await Register(token, patientId: "PTID00000003");
            Assert.That(res.Success, Is.True, res.Message);
            Assert.That(res.Created, Is.False);
            var p = await _context.PatientRegistrations.SingleAsync(x => x.PatientId == "PTID00000003");
            Assert.That(p.AbhaId, Is.EqualTo("91-0000-0000-0001"));
        }

        // ---------------------------------------------------------------- booking and admission keep the same rule

        [Test]
        public async Task BookingWithAnAbhaAlreadyOnFile_OpensThatPatient_AndMakesNoNewOne()
        {
            SeedPatient("PTID00000007", "Asha Kumari", "9000000001", abhaId: "91-0000-0000-0001");
            var found = await AppointmentBookingHelpers.FindOrCreatePatientAsync(_context,
                new Patient { FullName = "Asha K", Mobile = "9333333333", AbhaId = "91000000000001" }, _hospitalId, _staff, null, CancellationToken.None);
            Assert.That(found.PatientId, Is.EqualTo("PTID00000007"));
        }

        [Test]
        public async Task BookingNeverMovesAnAbha_OntoASecondPatient()
        {
            SeedPatient("PTID00000008", "Asha Kumari", "9000000001", abhaId: "91-0000-0000-0001");
            var other = SeedPatient("PTID00000009", "Ravi Kumar", "9444444444", sex: "Male", abhaId: "91-0000-0000-0002");
            var found = await AppointmentBookingHelpers.FindOrCreatePatientAsync(_context,
                new Patient { FullName = "Ravi Kumar", Mobile = "9444444444", AbhaId = "91-0000-0000-0003" }, _hospitalId, _staff, "PTID00000009", CancellationToken.None);
            Assert.That(found.PatientId, Is.EqualTo("PTID00000009"));
            Assert.That(found.AbhaId, Is.EqualTo("91-0000-0000-0002"), "a patient's own ABHA is not replaced by a different one");
        }

        [Test]
        public async Task AProofFromAnotherStaffMember_IsNotAccepted()
        {
            var other = HrAuthSeed.SeedMember(_context, _hospitalId);
            var token = _proofs.Issue(other, _hospitalId, Asha());
            var res = await Register(token);
            Assert.That(res.Success, Is.False);
        }
    }
}
