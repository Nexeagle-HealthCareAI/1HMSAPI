using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class GetNursingStationSummaryHandlerTests
    {
        private AppDbContext _context = null!;
        private GetNursingStationSummaryHandler _handler = null!;
        private Guid _hospitalId;
        private Guid _nurseUserId;
        private Guid _admissionId;
        private Guid _bedId;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetNursingStationSummaryHandler(_context);
            _hospitalId = Guid.NewGuid();
            _nurseUserId = Guid.NewGuid();
            _admissionId = Guid.NewGuid();
            _bedId = Guid.NewGuid();
        }

        [TearDown]
        public void TearDown()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        private void SeedAdmissionWithBed(string wardCode = "GENERAL")
        {
            _context.Admission.Add(new Admission
            {
                AdmissionId = _admissionId,
                HospitalId = _hospitalId,
                PatientId = "PT001",
                AdmissionNo = "ADM-1",
                AdmittedAt = DateTime.UtcNow,
                StatusCode = "ADMITTED",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            _context.BedMaster.Add(new BedMaster
            {
                BedId = _bedId,
                HospitalId = _hospitalId,
                WardCode = wardCode,
                WardName = wardCode,
                BedCode = "B1",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            _context.BedAssignment.Add(new BedAssignment
            {
                AssignmentId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                AdmissionId = _admissionId,
                BedId = _bedId,
                AssignedAt = DateTime.UtcNow,
                StatusCode = "ACTIVE",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
        }

        [Test]
        public async Task Handle_NoRosterAndNoDirectAssignment_ReturnsHasAssignmentsFalse()
        {
            var response = await _handler.Handle(new GetNursingStationSummaryRequestModel { HospitalId = _hospitalId, NurseUserId = _nurseUserId }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.HasAssignments, Is.False);
            Assert.That(response.Items, Is.Empty);
        }

        [Test]
        public async Task Handle_NoWardRoster_ButDirectPatientAssignment_StillShowsPatient()
        {
            SeedAdmissionWithBed();
            _context.PatientNurseAssignment.Add(new PatientNurseAssignment
            {
                PatientNurseAssignmentId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                AdmissionId = _admissionId,
                NurseUserId = _nurseUserId,
                ShiftCode = "MORNING",
                StatusCode = "ACTIVE",
                AssignedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetNursingStationSummaryRequestModel { HospitalId = _hospitalId, NurseUserId = _nurseUserId }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.HasAssignments, Is.True);
            Assert.That(response.Items, Has.Count.EqualTo(1));
            Assert.That(response.Items[0].AdmissionId, Is.EqualTo(_admissionId));
            Assert.That(response.Items[0].BedCode, Is.EqualTo("B1"));
        }

        [Test]
        public async Task Handle_DirectAssignment_OtherShiftOnly_NotIncludedWhenFilteredToDifferentShift()
        {
            SeedAdmissionWithBed();
            _context.PatientNurseAssignment.Add(new PatientNurseAssignment
            {
                PatientNurseAssignmentId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                AdmissionId = _admissionId,
                NurseUserId = _nurseUserId,
                ShiftCode = "NIGHT",
                StatusCode = "ACTIVE",
                AssignedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetNursingStationSummaryRequestModel { HospitalId = _hospitalId, NurseUserId = _nurseUserId, ShiftCode = "MORNING" }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.HasAssignments, Is.False);
        }

        [Test]
        public async Task Handle_WardRoster_StillWorksAsBefore()
        {
            SeedAdmissionWithBed("ICU");
            _context.NurseShiftAssignment.Add(new NurseShiftAssignment
            {
                NurseShiftAssignmentId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                NurseUserId = _nurseUserId,
                WardCode = "ICU",
                ShiftCode = "MORNING",
                StatusCode = "ACTIVE",
                AssignedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetNursingStationSummaryRequestModel { HospitalId = _hospitalId, NurseUserId = _nurseUserId }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.HasAssignments, Is.True);
            Assert.That(response.Items, Has.Count.EqualTo(1));
            Assert.That(response.Items[0].AdmissionId, Is.EqualTo(_admissionId));
        }
    }
}
