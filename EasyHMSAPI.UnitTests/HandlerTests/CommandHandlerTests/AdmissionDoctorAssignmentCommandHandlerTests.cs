using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class AdmissionDoctorAssignmentCommandHandlerTests
    {
        private AppDbContext _context = null!;
        private AdmissionDoctorAssignmentCommandHandlers _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new AdmissionDoctorAssignmentCommandHandlers(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private (Guid hospitalId, Doctor doctor1, Doctor doctor2, Admission admission) SeedBasics(string admissionStatus = "ADMITTED")
        {
            var hospitalId = Guid.NewGuid();
            _context.Hospitals.Add(new Hospital
            {
                HospitalID = hospitalId, Name = "Hosp", Email = "e@m.com", Type = "General", RegistrationNumber = "REG001",
                Contact = "1234567890", Location = "Test Location", City = "Test City", State = "Test State",
                Country = "Test Country", Pincode = "123456", CreatedByUserID = Guid.NewGuid(),
            });
            _context.SaveChanges();

            var user1 = TestDataFactory.SeedUser(_context, email: "doc1@example.com", phone: "1111111111");
            var doctor1 = TestDataFactory.SeedDoctor(_context, user1);
            TestDataFactory.SeedDoctorDepartment(_context, doctor1.DoctorID, hospitalId);

            var user2 = TestDataFactory.SeedUser(_context, email: "doc2@example.com", phone: "2222222222");
            var doctor2 = TestDataFactory.SeedDoctor(_context, user2);
            TestDataFactory.SeedDoctorDepartment(_context, doctor2.DoctorID, hospitalId);

            var admission = new Admission
            {
                AdmissionId = Guid.NewGuid(),
                HospitalId = hospitalId,
                PatientId = "PTID00000001",
                AdmissionNo = "ADM-1",
                AdmittedAt = DateTime.UtcNow,
                PrimaryDoctorId = doctor1.DoctorID,
                StatusCode = admissionStatus,
            };
            _context.Admission.Add(admission);
            _context.SaveChanges();

            return (hospitalId, doctor1, doctor2, admission);
        }

        [Test]
        public async Task Handle_ValidRequest_ReleasesOldRowAndCreatesNewActiveRow_AndUpdatesAdmissionPrimaryDoctorId()
        {
            var (hospitalId, doctor1, doctor2, admission) = SeedBasics();
            // Seed the pre-existing ACTIVE row for doctor1, as AdmitPatientHandler now does at admit time.
            _context.AdmissionDoctorAssignment.Add(new AdmissionDoctorAssignment
            {
                AssignmentId = Guid.NewGuid(), HospitalId = hospitalId, AdmissionId = admission.AdmissionId,
                DoctorId = doctor1.DoctorID, AssignedAt = DateTime.UtcNow, StatusCode = "ACTIVE", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new ChangeAdmittingDoctorRequestModel
            {
                HospitalId = hospitalId, AdmissionId = admission.AdmissionId, DoctorId = doctor2.DoctorID, LoggedInUserName = "Front Desk",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            Assert.That(response.DoctorId, Is.EqualTo(doctor2.DoctorID));

            var updatedAdmission = await _context.Admission.FindAsync(admission.AdmissionId);
            Assert.That(updatedAdmission!.PrimaryDoctorId, Is.EqualTo(doctor2.DoctorID));

            var rows = _context.AdmissionDoctorAssignment.Where(a => a.AdmissionId == admission.AdmissionId).ToList();
            Assert.That(rows, Has.Count.EqualTo(2));
            var oldRow = rows.Single(r => r.DoctorId == doctor1.DoctorID);
            Assert.That(oldRow.StatusCode, Is.EqualTo("REPLACED"));
            Assert.That(oldRow.UnassignedAt, Is.Not.Null);
            var newRow = rows.Single(r => r.DoctorId == doctor2.DoctorID);
            Assert.That(newRow.StatusCode, Is.EqualTo("ACTIVE"));
            Assert.That(newRow.UnassignedAt, Is.Null);
        }

        [Test]
        public async Task Handle_SameDoctor_ReturnsFailureWithoutChurningHistory()
        {
            var (hospitalId, doctor1, _, admission) = SeedBasics();

            var response = await _handler.Handle(new ChangeAdmittingDoctorRequestModel
            {
                HospitalId = hospitalId, AdmissionId = admission.AdmissionId, DoctorId = doctor1.DoctorID, LoggedInUserName = "Front Desk",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(_context.AdmissionDoctorAssignment.Count(a => a.AdmissionId == admission.AdmissionId), Is.EqualTo(0));
        }

        [Test]
        public async Task Handle_AdmissionNotActive_ReturnsFailure()
        {
            var (hospitalId, _, doctor2, admission) = SeedBasics(admissionStatus: "DISCHARGED");

            var response = await _handler.Handle(new ChangeAdmittingDoctorRequestModel
            {
                HospitalId = hospitalId, AdmissionId = admission.AdmissionId, DoctorId = doctor2.DoctorID, LoggedInUserName = "Front Desk",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("closed"));
        }

        [Test]
        public async Task Handle_DoctorNotInHospital_ReturnsFailure()
        {
            var (hospitalId, _, _, admission) = SeedBasics();
            var strangerUser = TestDataFactory.SeedUser(_context, email: "stranger@example.com", phone: "3333333333");
            var strangerDoctor = TestDataFactory.SeedDoctor(_context, strangerUser);
            // Deliberately no SeedDoctorDepartment for this hospital.

            var response = await _handler.Handle(new ChangeAdmittingDoctorRequestModel
            {
                HospitalId = hospitalId, AdmissionId = admission.AdmissionId, DoctorId = strangerDoctor.DoctorID, LoggedInUserName = "Front Desk",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("not found"));
        }

        [Test]
        public async Task Handle_AdmissionNotFound_ReturnsFailure()
        {
            var (hospitalId, _, doctor2, _) = SeedBasics();

            var response = await _handler.Handle(new ChangeAdmittingDoctorRequestModel
            {
                HospitalId = hospitalId, AdmissionId = Guid.NewGuid(), DoctorId = doctor2.DoctorID, LoggedInUserName = "Front Desk",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("not found"));
        }
    }
}
