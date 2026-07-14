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
    public class AdmissionReferrerAssignmentCommandHandlerTests
    {
        private AppDbContext _context = null!;
        private AdmissionReferrerAssignmentCommandHandlers _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new AdmissionReferrerAssignmentCommandHandlers(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private (Guid hospitalId, Referrer referrer1, Referrer referrer2, Admission admission) SeedBasics(string admissionStatus = "ADMITTED")
        {
            var hospitalId = Guid.NewGuid();
            _context.Hospitals.Add(new Hospital
            {
                HospitalID = hospitalId, Name = "Hosp", Email = "e@m.com", Type = "General", RegistrationNumber = "REG001",
                Contact = "1234567890", Location = "Test Location", City = "Test City", State = "Test State",
                Country = "Test Country", Pincode = "123456", CreatedByUserID = Guid.NewGuid(),
            });

            var referrer1 = new Referrer
            {
                ReferrerId = Guid.NewGuid(), HospitalId = hospitalId, ReferrerName = "Dr. Amit", ReferrerType = "DOCTOR",
                DefaultRatePercent = 0, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            var referrer2 = new Referrer
            {
                ReferrerId = Guid.NewGuid(), HospitalId = hospitalId, ReferrerName = "Samim Khan", ReferrerType = "REFERRER",
                DefaultRatePercent = 0, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            _context.Referrers.AddRange(referrer1, referrer2);

            var admission = new Admission
            {
                AdmissionId = Guid.NewGuid(),
                HospitalId = hospitalId,
                PatientId = "PTID00000001",
                AdmissionNo = "ADM-1",
                AdmittedAt = DateTime.UtcNow,
                ReferralSource = "DOCTOR",
                ReferredByReferrerId = referrer1.ReferrerId,
                ReferralName = referrer1.ReferrerName,
                StatusCode = admissionStatus,
            };
            _context.Admission.Add(admission);
            _context.SaveChanges();

            return (hospitalId, referrer1, referrer2, admission);
        }

        [Test]
        public async Task Handle_ValidRequest_ReleasesOldRowAndCreatesNewActiveRow_AndUpdatesAdmissionReferralFields()
        {
            var (hospitalId, referrer1, referrer2, admission) = SeedBasics();
            // Seed the pre-existing ACTIVE row for referrer1, as the admit flow would.
            _context.AdmissionReferrerAssignment.Add(new AdmissionReferrerAssignment
            {
                AssignmentId = Guid.NewGuid(), HospitalId = hospitalId, AdmissionId = admission.AdmissionId,
                ReferralSource = "DOCTOR", ReferrerId = referrer1.ReferrerId, ReferrerName = referrer1.ReferrerName, ReferrerType = "DOCTOR",
                AssignedAt = DateTime.UtcNow, StatusCode = "ACTIVE", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new ChangeAdmissionReferrerRequestModel
            {
                HospitalId = hospitalId, AdmissionId = admission.AdmissionId,
                ReferralSource = "OTHER", ReferrerId = referrer2.ReferrerId, ReferrerName = referrer2.ReferrerName, ReferrerType = "REFERRER",
                LoggedInUserName = "Front Desk",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);

            var updatedAdmission = await _context.Admission.FindAsync(admission.AdmissionId);
            Assert.That(updatedAdmission!.ReferralSource, Is.EqualTo("OTHER"));
            Assert.That(updatedAdmission.ReferredByReferrerId, Is.EqualTo(referrer2.ReferrerId));
            Assert.That(updatedAdmission.ReferralName, Is.EqualTo("Samim Khan"));

            var rows = _context.AdmissionReferrerAssignment.Where(a => a.AdmissionId == admission.AdmissionId).ToList();
            Assert.That(rows, Has.Count.EqualTo(2));
            var oldRow = rows.Single(r => r.ReferrerId == referrer1.ReferrerId);
            Assert.That(oldRow.StatusCode, Is.EqualTo("REPLACED"));
            Assert.That(oldRow.UnassignedAt, Is.Not.Null);
            var newRow = rows.Single(r => r.ReferrerId == referrer2.ReferrerId);
            Assert.That(newRow.StatusCode, Is.EqualTo("ACTIVE"));
            Assert.That(newRow.UnassignedAt, Is.Null);
        }

        [Test]
        public async Task Handle_SwitchToSelf_ClearsReferrerFields()
        {
            var (hospitalId, referrer1, _, admission) = SeedBasics();

            var response = await _handler.Handle(new ChangeAdmissionReferrerRequestModel
            {
                HospitalId = hospitalId, AdmissionId = admission.AdmissionId, ReferralSource = "SELF", LoggedInUserName = "Front Desk",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            var updatedAdmission = await _context.Admission.FindAsync(admission.AdmissionId);
            Assert.That(updatedAdmission!.ReferralSource, Is.EqualTo("SELF"));
            Assert.That(updatedAdmission.ReferredByReferrerId, Is.Null);
            Assert.That(updatedAdmission.ReferralName, Is.Null);
        }

        [Test]
        public async Task Handle_SameReferrer_ReturnsFailureWithoutChurningHistory()
        {
            var (hospitalId, referrer1, _, admission) = SeedBasics();

            var response = await _handler.Handle(new ChangeAdmissionReferrerRequestModel
            {
                HospitalId = hospitalId, AdmissionId = admission.AdmissionId,
                ReferralSource = "DOCTOR", ReferrerId = referrer1.ReferrerId, ReferrerName = referrer1.ReferrerName,
                LoggedInUserName = "Front Desk",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(_context.AdmissionReferrerAssignment.Count(a => a.AdmissionId == admission.AdmissionId), Is.EqualTo(0));
        }

        [Test]
        public async Task Handle_DoctorSourceWithoutReferrerId_ReturnsFailure()
        {
            var (hospitalId, _, _, admission) = SeedBasics();

            var response = await _handler.Handle(new ChangeAdmissionReferrerRequestModel
            {
                HospitalId = hospitalId, AdmissionId = admission.AdmissionId, ReferralSource = "DOCTOR", LoggedInUserName = "Front Desk",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("must be selected"));
        }

        [Test]
        public async Task Handle_AdmissionNotActive_ReturnsFailure()
        {
            var (hospitalId, _, referrer2, admission) = SeedBasics(admissionStatus: "DISCHARGED");

            var response = await _handler.Handle(new ChangeAdmissionReferrerRequestModel
            {
                HospitalId = hospitalId, AdmissionId = admission.AdmissionId,
                ReferralSource = "OTHER", ReferrerId = referrer2.ReferrerId, ReferrerName = referrer2.ReferrerName,
                LoggedInUserName = "Front Desk",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("closed"));
        }

        [Test]
        public async Task Handle_ReferrerNotInHospital_ReturnsFailure()
        {
            var (hospitalId, _, _, admission) = SeedBasics();
            var strangerReferrer = new Referrer
            {
                ReferrerId = Guid.NewGuid(), HospitalId = Guid.NewGuid(), ReferrerName = "Stranger", ReferrerType = "REFERRER",
                DefaultRatePercent = 0, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            _context.Referrers.Add(strangerReferrer);
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new ChangeAdmissionReferrerRequestModel
            {
                HospitalId = hospitalId, AdmissionId = admission.AdmissionId,
                ReferralSource = "OTHER", ReferrerId = strangerReferrer.ReferrerId, ReferrerName = strangerReferrer.ReferrerName,
                LoggedInUserName = "Front Desk",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("not found"));
        }

        [Test]
        public async Task Handle_AdmissionNotFound_ReturnsFailure()
        {
            var (hospitalId, _, referrer2, _) = SeedBasics();

            var response = await _handler.Handle(new ChangeAdmissionReferrerRequestModel
            {
                HospitalId = hospitalId, AdmissionId = Guid.NewGuid(),
                ReferralSource = "OTHER", ReferrerId = referrer2.ReferrerId, ReferrerName = referrer2.ReferrerName,
                LoggedInUserName = "Front Desk",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("not found"));
        }
    }
}
