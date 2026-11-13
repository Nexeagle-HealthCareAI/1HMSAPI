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
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    // Beds used to accept anyone: inactive, blocked, being cleaned, wrong-gender ward... and the status column
    // was never kept in step with the real assignments, so the "occupied beds can't be deleted" guards never fired.
    [TestFixture]
    public class BedAssignmentGuardTests
    {
        private AppDbContext _context = null!;
        private BedAssignmentCommandHandlers _handler = null!;
        private Guid _hospitalId;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new BedAssignmentCommandHandlers(_context);
            _hospitalId = Guid.NewGuid();
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private BedMaster SeedBed(string code = "B1", string? status = "AVAILABLE", bool active = true, string? gender = "NONE")
        {
            var bed = new BedMaster
            {
                BedId = Guid.NewGuid(), HospitalId = _hospitalId, BedCode = code, StatusCode = status, IsActive = active,
                GenderRestriction = gender, WardRoomDailyRate = 1000, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            _context.BedMaster.Add(bed);
            _context.SaveChanges();
            return bed;
        }

        private Admission SeedAdmission(string patientId, string? sex)
        {
            var patient = TestEntityFactory.CreatePatientRegistration(_hospitalId, patientId);
            patient.Sex = sex;
            _context.PatientRegistrations.Add(patient);
            var admission = new Admission
            {
                AdmissionId = Guid.NewGuid(), HospitalId = _hospitalId, PatientId = patientId, AdmissionNo = "A-" + patientId,
                AdmittedAt = DateTime.UtcNow, StatusCode = IpdConstants.AdmissionStatus.Admitted, PayerType = "CASH",
            };
            _context.Admission.Add(admission);
            _context.SaveChanges();
            return admission;
        }

        private Task<EasyHMSAPI.Application.ResponseModels.CommandResponseModels.AssignBedResponseModel> Assign(Admission a, BedMaster b) =>
            _handler.Handle(new AssignBedRequestModel { HospitalId = _hospitalId, AdmissionId = a.AdmissionId, BedId = b.BedId, LoggedInUserName = "Nurse" }, CancellationToken.None);

        private BedMaster Reload(BedMaster b) { _context.Entry(b).Reload(); return b; }

        [Test]
        public async Task Assign_AvailableBed_MarksItOccupied()
        {
            var bed = SeedBed();
            var response = await Assign(SeedAdmission("P1", "Male"), bed);

            Assert.That(response.Success, Is.True, response.Message);
            Assert.That(Reload(bed).StatusCode, Is.EqualTo(IpdConstants.BedStatus.Occupied));
            Assert.That(bed.LastStatusAt, Is.Not.Null);
        }

        [Test]
        public async Task Assign_BedWithNoStatus_IsTreatedAsAvailable()
        {
            var bed = SeedBed(status: null);
            var response = await Assign(SeedAdmission("P1", "Male"), bed);
            Assert.That(response.Success, Is.True, response.Message);
        }

        [TestCase("CLEANING", "cleaned")]
        [TestCase("BLOCKED", "blocked")]
        [TestCase("RESERVED", "reserved")]
        [TestCase("OCCUPIED", "occupied")]
        public async Task Assign_BedThatIsNotAvailable_IsRefused(string status, string messagePart)
        {
            var bed = SeedBed(status: status);
            var response = await Assign(SeedAdmission("P1", "Male"), bed);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain(messagePart));
            Assert.That(_context.BedAssignment.Any(), Is.False);
        }

        [Test]
        public async Task Assign_InactiveBed_IsRefused()
        {
            var bed = SeedBed(active: false);
            var response = await Assign(SeedAdmission("P1", "Male"), bed);
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("inactive"));
        }

        [Test]
        public async Task Assign_BedAlreadyHoldingAPatient_IsRefused_EvenIfStatusSaysAvailable()
        {
            // the drifted-status case that used to rely on the DB index alone
            var bed = SeedBed(status: "AVAILABLE");
            var first = await Assign(SeedAdmission("P1", "Male"), bed);
            Assert.That(first.Success, Is.True);
            bed.StatusCode = "AVAILABLE";   // simulate the drift
            _context.SaveChanges();

            var second = await Assign(SeedAdmission("P2", "Male"), bed);
            Assert.That(second.Success, Is.False);
            Assert.That(second.Message, Does.Contain("occupied"));
        }

        [Test]
        public async Task Assign_MaleOnlyBed_RefusesFemalePatient()
        {
            var bed = SeedBed(gender: "MALE_ONLY");
            var response = await Assign(SeedAdmission("P1", "Female"), bed);
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("male"));
        }

        [Test]
        public async Task Assign_FemaleOnlyBed_AcceptsFemalePatient()
        {
            var bed = SeedBed(gender: "FEMALE_ONLY");
            var response = await Assign(SeedAdmission("P1", "F"), bed);
            Assert.That(response.Success, Is.True, response.Message);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("Other")]
        public async Task Assign_RestrictedBed_RefusesWhenSexNotRecorded(string? sex)
        {
            var bed = SeedBed(gender: "FEMALE_ONLY");
            var response = await Assign(SeedAdmission("P1", sex), bed);
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("not recorded"));
        }

        [Test]
        public async Task Assign_UnrestrictedBed_AcceptsAnyoneIncludingUnknownSex()
        {
            var bed = SeedBed(gender: "NONE");
            var response = await Assign(SeedAdmission("P1", null), bed);
            Assert.That(response.Success, Is.True, response.Message);
        }

        [Test]
        public async Task Release_MarksBedCleaning_NotAvailable()
        {
            var bed = SeedBed();
            var admission = SeedAdmission("P1", "Male");
            await Assign(admission, bed);

            var response = await _handler.Handle(new ReleaseBedRequestModel { HospitalId = _hospitalId, AdmissionId = admission.AdmissionId, LoggedInUserName = "Nurse" }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            Assert.That(Reload(bed).StatusCode, Is.EqualTo(IpdConstants.BedStatus.Cleaning));

            // and the next patient cannot be put in until it is marked available
            var next = await Assign(SeedAdmission("P2", "Male"), bed);
            Assert.That(next.Success, Is.False);
        }

        [Test]
        public async Task Transfer_ValidatesDestination_AndMovesTheStatuses()
        {
            var from = SeedBed("B1");
            var to = SeedBed("B2");
            var blocked = SeedBed("B3", status: "BLOCKED");
            var admission = SeedAdmission("P1", "Male");
            await Assign(admission, from);

            var refused = await _handler.Handle(new TransferBedRequestModel { HospitalId = _hospitalId, AdmissionId = admission.AdmissionId, NewBedId = blocked.BedId, LoggedInUserName = "N" }, CancellationToken.None);
            Assert.That(refused.Success, Is.False);
            Assert.That(Reload(from).StatusCode, Is.EqualTo(IpdConstants.BedStatus.Occupied), "a refused transfer must not touch the current bed");

            var ok = await _handler.Handle(new TransferBedRequestModel { HospitalId = _hospitalId, AdmissionId = admission.AdmissionId, NewBedId = to.BedId, LoggedInUserName = "N" }, CancellationToken.None);
            Assert.That(ok.Success, Is.True, ok.Message);
            Assert.That(Reload(from).StatusCode, Is.EqualTo(IpdConstants.BedStatus.Cleaning));
            Assert.That(Reload(to).StatusCode, Is.EqualTo(IpdConstants.BedStatus.Occupied));
        }

        [Test]
        public async Task Transfer_ToWrongGenderBed_IsRefused()
        {
            var from = SeedBed("B1");
            var to = SeedBed("B2", gender: "FEMALE_ONLY");
            var admission = SeedAdmission("P1", "Male");
            await Assign(admission, from);

            var response = await _handler.Handle(new TransferBedRequestModel { HospitalId = _hospitalId, AdmissionId = admission.AdmissionId, NewBedId = to.BedId, LoggedInUserName = "N" }, CancellationToken.None);
            Assert.That(response.Success, Is.False);
            Assert.That(_context.BedAssignment.Count(a => a.StatusCode == "ACTIVE"), Is.EqualTo(1));
        }

        // ---- bed master: a bed with a patient in it cannot be deactivated or re-statused ----

        private async Task<BedMaster> OccupiedBedWithPatient()
        {
            var bed = SeedBed();
            var response = await Assign(SeedAdmission("P1", "Male"), bed);
            Assert.That(response.Success, Is.True, response.Message);
            bed.StatusCode = "AVAILABLE";   // drifted status: the guard must use the real assignment
            _context.SaveChanges();
            return bed;
        }

        private UpsertBedMasterHandler MasterHandler() => new(_context, new Mock<EasyHMSAPI.Application.Helpers.Interfaces.ISubscriptionLimitHelper>().Object);

        [Test]
        public async Task Deactivate_BedWithActivePatient_IsRefused_EvenIfStatusDrifted()
        {
            var bed = await OccupiedBedWithPatient();

            var ex = Assert.ThrowsAsync<InvalidOperationException>(() => MasterHandler().Handle(
                new UpsertBedMasterRequestModel { HospitalId = _hospitalId, BedId = bed.BedId, IsActive = false, LoggedInUserName = "Admin" }, CancellationToken.None));

            Assert.That(ex!.Message, Does.Contain("occupied"));
            Assert.That(Reload(bed).IsActive, Is.True);
        }

        [Test]
        public async Task ChangingStatusOfBedWithActivePatient_IsRefused()
        {
            var bed = await OccupiedBedWithPatient();

            var ex = Assert.ThrowsAsync<InvalidOperationException>(() => MasterHandler().Handle(
                new UpsertBedMasterRequestModel { HospitalId = _hospitalId, BedId = bed.BedId, IsActive = true, StatusCode = "BLOCKED", LoggedInUserName = "Admin" }, CancellationToken.None));

            Assert.That(ex!.Message, Does.Contain("patient"));
        }

        [Test]
        public async Task BulkDelete_SkipsBedsWithActivePatient_AndDeactivatesTheRest()
        {
            var occupied = await OccupiedBedWithPatient();
            var free = SeedBed("FREE");

            var response = await new BulkDeleteBedMasterHandler(_context).Handle(
                new BulkDeleteBedMasterRequestModel { HospitalId = _hospitalId, BedIds = { occupied.BedId, free.BedId }, LoggedInUserName = "Admin" }, CancellationToken.None);

            Assert.That(response.Blocked.Select(b => b.BedId), Does.Contain(occupied.BedId));
            Assert.That(response.Deactivated, Does.Contain(free.BedId));
            Assert.That(Reload(occupied).IsActive, Is.True);
            Assert.That(Reload(free).IsActive, Is.False);
        }

        [TestCase("Male", "M")]
        [TestCase("FEMALE", "F")]
        [TestCase(" f ", "F")]
        [TestCase("Other", null)]
        [TestCase(null, null)]
        public void NormaliseSex(string? raw, string? expected) => Assert.That(BedOccupancy.NormaliseSex(raw), Is.EqualTo(expected));
    }
}
