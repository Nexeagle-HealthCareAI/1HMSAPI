using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Common;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using MediatR;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    // A transfusion is the one place in the HMS where a data-entry slip can kill. These pin every bedside
    // gate: reserved for THIS patient, compatible crossmatch, ABO/Rh suits the recorded group, signed consent,
    // and a real second person as witness.
    [TestFixture]
    public class TransfusionGateTests
    {
        private AppDbContext _context = null!;
        private BloodBankCommandHandlers _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new BloodBankCommandHandlers(_context, new Mock<IMediator>().Object);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private sealed record Scenario(Guid HospitalId, BloodBag Bag, Admission Admission, Guid Nurse, Guid Witness);

        private Scenario Seed(
            string bagGroup = "A_POS",
            string patientGroup = "A+",
            string component = IpdConstants.BloodComponent.Prbc,
            string crossmatch = IpdConstants.CrossmatchResult.Compatible,
            string status = IpdConstants.BloodBagStatus.Reserved,
            bool reservedForThisAdmission = true,
            bool consent = true)
        {
            var hospitalId = Guid.NewGuid();
            var admission = new Admission
            {
                AdmissionId = Guid.NewGuid(), HospitalId = hospitalId, PatientId = "PTID1", AdmissionNo = "ADM-1",
                AdmittedAt = DateTime.UtcNow.AddDays(-1), StatusCode = "ADMITTED", PayerType = "CASH",
            };
            _context.Admission.Add(admission);

            var patient = TestEntityFactory.CreatePatientRegistration(hospitalId, "PTID1");
            patient.BloodGroup = patientGroup;
            _context.PatientRegistrations.Add(patient);

            var bag = new BloodBag
            {
                BloodBagId = Guid.NewGuid(), HospitalId = hospitalId, BagNumber = "BAG-9", Component = component,
                BloodGroup = bagGroup, VolumeMl = 300, CollectedAt = DateTime.UtcNow.AddDays(-5), ExpiresAt = DateTime.UtcNow.AddDays(20),
                Status = status, CrossmatchResult = crossmatch,
                ReservedForAdmissionId = reservedForThisAdmission ? admission.AdmissionId : Guid.NewGuid(),
            };
            _context.BloodBag.Add(bag);

            if (consent)
            {
                _context.ConsentRecord.Add(new ConsentRecord
                {
                    ConsentRecordId = Guid.NewGuid(), HospitalId = hospitalId, AdmissionId = admission.AdmissionId,
                    ConsentTemplateId = Guid.NewGuid(), TemplateTypeCode = IpdConstants.ConsentTypeCode.BloodTransfusion,
                    SignedByName = "Patient", SignerRelation = "SELF", SignedAt = DateTime.UtcNow.AddHours(-3), CreatedAt = DateTime.UtcNow.AddHours(-3),
                });
            }
            _context.SaveChanges();

            var nurse = HrAuthSeed.SeedMember(_context, hospitalId);
            var witness = HrAuthSeed.SeedMember(_context, hospitalId);
            return new Scenario(hospitalId, bag, admission, nurse, witness);
        }

        private RecordTransfusionRequestModel Request(Scenario s, Guid? witness = null, bool noWitness = false) => new()
        {
            HospitalId = s.HospitalId, BloodBagId = s.Bag.BloodBagId, AdmissionId = s.Admission.AdmissionId,
            StartedAt = DateTime.UtcNow.AddMinutes(-10), VolumeGivenMl = 100,
            LoggedInUserId = s.Nurse, LoggedInUserName = "Nurse N",
            WitnessUserId = noWitness ? null : witness ?? s.Witness, WitnessName = "typed name",
        };

        private async Task AssertRefused(Scenario s, RecordTransfusionRequestModel request, string messagePart)
        {
            var response = await _handler.Handle(request, CancellationToken.None);
            Assert.That(response.Success, Is.False, "the transfusion should have been refused");
            Assert.That(response.Message, Does.Contain(messagePart));
            Assert.That(_context.TransfusionEvent.Any(), Is.False, "nothing may be recorded");
            Assert.That(_context.BloodBag.Single(b => b.BloodBagId == s.Bag.BloodBagId).Status, Is.EqualTo(IpdConstants.BloodBagStatus.Reserved));
        }

        [Test]
        public async Task AllGatesPassed_RecordsTransfusion_WithWitnessFromProfile()
        {
            var s = Seed();
            _context.UserProfiles.Add(new UserProfile { UserID = s.Witness, FullName = "Sister W" });
            _context.SaveChanges();

            var response = await _handler.Handle(Request(s), CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            var saved = _context.TransfusionEvent.Single();
            Assert.That(saved.WitnessName, Is.EqualTo("Sister W"));
            Assert.That(saved.WitnessUserId, Is.EqualTo(s.Witness));
            Assert.That(_context.BloodBag.Single().Status, Is.EqualTo(IpdConstants.BloodBagStatus.Transfused));
        }

        [Test]
        public async Task AvailableBag_MustBeReservedFirst()
        {
            var s = Seed(status: IpdConstants.BloodBagStatus.Available);
            var response = await _handler.Handle(Request(s), CancellationToken.None);
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("Reserve and crossmatch"));
            Assert.That(_context.TransfusionEvent.Any(), Is.False);
        }

        [Test]
        public async Task BagReservedForAnotherPatient_IsRefused()
        {
            var s = Seed(reservedForThisAdmission: false);
            await AssertRefused(s, Request(s), "different patient");
        }

        [TestCase(IpdConstants.CrossmatchResult.NotDone, "No compatible crossmatch")]
        [TestCase(IpdConstants.CrossmatchResult.Incompatible, "INCOMPATIBLE")]
        public async Task CrossmatchNotCompatible_IsRefused(string crossmatch, string message)
        {
            var s = Seed(crossmatch: crossmatch);
            await AssertRefused(s, Request(s), message);
        }

        [Test]
        public async Task AboMismatch_IsRefused()
        {
            var s = Seed(bagGroup: "B_POS", patientGroup: "A+");
            await AssertRefused(s, Request(s), "not compatible");
        }

        [Test]
        public async Task RhPositiveCells_ForRhNegativePatient_AreRefused()
        {
            var s = Seed(bagGroup: "O_POS", patientGroup: "A-");
            await AssertRefused(s, Request(s), "Rh-negative");
        }

        [Test]
        public async Task ONegativeRedCells_AreUniversal()
        {
            var s = Seed(bagGroup: "O_NEG", patientGroup: "AB+");
            var response = await _handler.Handle(Request(s), CancellationToken.None);
            Assert.That(response.Success, Is.True, response.Message);
        }

        [Test]
        public async Task WholeBlood_MustBeAboIdentical()
        {
            var s = Seed(bagGroup: "O_NEG", patientGroup: "A+", component: IpdConstants.BloodComponent.Whole);
            await AssertRefused(s, Request(s), "same ABO");
        }

        [TestCase("Unknown")]
        [TestCase("")]
        [TestCase(null)]
        public async Task PatientGroupNotRecorded_IsRefused(string? group)
        {
            var s = Seed(patientGroup: group!);
            await AssertRefused(s, Request(s), "not recorded");
        }

        [Test]
        public async Task NoSignedConsent_IsRefused()
        {
            var s = Seed(consent: false);
            await AssertRefused(s, Request(s), "consent");
        }

        [Test]
        public async Task ConsentSignedAfterTheTransfusionStarted_DoesNotCount()
        {
            var s = Seed(consent: false);
            _context.ConsentRecord.Add(new ConsentRecord
            {
                ConsentRecordId = Guid.NewGuid(), HospitalId = s.HospitalId, AdmissionId = s.Admission.AdmissionId,
                ConsentTemplateId = Guid.NewGuid(), TemplateTypeCode = IpdConstants.ConsentTypeCode.BloodTransfusion,
                SignedByName = "Patient", SignerRelation = "SELF", SignedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow,
            });
            _context.SaveChanges();

            var request = Request(s);
            request.StartedAt = DateTime.UtcNow.AddHours(-2);   // consent was signed after this
            await AssertRefused(s, request, "consent");
        }

        [Test]
        public async Task SomeOtherConsentType_DoesNotCount()
        {
            var s = Seed(consent: false);
            _context.ConsentRecord.Add(new ConsentRecord
            {
                ConsentRecordId = Guid.NewGuid(), HospitalId = s.HospitalId, AdmissionId = s.Admission.AdmissionId,
                ConsentTemplateId = Guid.NewGuid(), TemplateTypeCode = IpdConstants.ConsentTypeCode.GeneralAdmission,
                SignedByName = "Patient", SignerRelation = "SELF", SignedAt = DateTime.UtcNow.AddHours(-5), CreatedAt = DateTime.UtcNow.AddHours(-5),
            });
            _context.SaveChanges();
            await AssertRefused(s, Request(s), "consent");
        }

        [Test]
        public async Task WitnessMissing_IsRefused()
        {
            var s = Seed();
            await AssertRefused(s, Request(s, noWitness: true), "witnessed");
        }

        [Test]
        public async Task WitnessSameAsAdministrator_IsRefused()
        {
            var s = Seed();
            await AssertRefused(s, Request(s, witness: s.Nurse), "different person");
        }

        [Test]
        public async Task WitnessFromAnotherHospital_IsRefused()
        {
            var s = Seed();
            var outsider = HrAuthSeed.SeedMember(_context, Guid.NewGuid());
            await AssertRefused(s, Request(s, witness: outsider), "staff member of this hospital");
        }

        [Test]
        public async Task AnonymousCaller_IsRefused()
        {
            var s = Seed();
            var request = Request(s);
            request.LoggedInUserId = null;
            await AssertRefused(s, request, "identify the signed-in user");
        }

        [Test]
        public async Task VolumeAboveBagVolume_IsRefused()
        {
            var s = Seed();
            var request = Request(s);
            request.VolumeGivenMl = 900;
            await AssertRefused(s, request, "Volume given");
        }

        [TestCase("A_POS", "A", true)]
        [TestCase("AB_NEG", "AB", false)]
        [TestCase("o-", "O", false)]
        [TestCase("B positive", "B", true)]
        [TestCase(" O + ", "O", true)]
        public void Parse_AcceptsBothStorageFormats(string raw, string abo, bool rhPositive)
        {
            var group = BloodCompatibility.Parse(raw);
            Assert.That(group, Is.Not.Null);
            Assert.That(group!.Value.Abo, Is.EqualTo(abo));
            Assert.That(group.Value.RhPositive, Is.EqualTo(rhPositive));
        }

        [TestCase("Unknown")]
        [TestCase("A")]
        [TestCase("")]
        [TestCase("C+")]
        public void Parse_RejectsAnythingThatIsNotADefiniteGroup(string raw) => Assert.That(BloodCompatibility.Parse(raw), Is.Null);

        [TestCase("FFP", "AB_POS", "O+", null)]       // AB plasma goes to everyone
        [TestCase("FFP", "O_POS", "A+", "not compatible")]  // O plasma only to O
        [TestCase("PLATELET", "A_POS", "A-", null)]   // Rh is not a plasma barrier
        [TestCase("PRBC", "A_NEG", "AB+", null)]
        [TestCase("PRBC", "AB_POS", "A+", "not compatible")]
        public void ComponentRules(string component, string bag, string patient, string? expectedProblem)
        {
            var problem = BloodCompatibility.CheckBagForPatient(component, bag, patient);
            if (expectedProblem == null) Assert.That(problem, Is.Null);
            else Assert.That(problem, Does.Contain(expectedProblem));
        }
    }
}
