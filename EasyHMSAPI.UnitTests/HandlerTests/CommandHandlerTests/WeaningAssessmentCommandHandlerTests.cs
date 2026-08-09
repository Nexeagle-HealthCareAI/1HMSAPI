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
    public class WeaningAssessmentCommandHandlerTests
    {
        private AppDbContext _context = null!;
        private WeaningAssessmentCommandHandlers _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new WeaningAssessmentCommandHandlers(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        private Admission SeedAdmission()
        {
            var admission = new Admission
            {
                AdmissionId = Guid.NewGuid(),
                HospitalId = Guid.NewGuid(),
                PatientId = "PTID00000001",
                AdmissionNo = "ADM-1",
                AdmittedAt = DateTime.UtcNow,
                StatusCode = "ADMITTED",
                PayerType = "CASH",
            };
            _context.Admission.Add(admission);
            _context.SaveChanges();
            return admission;
        }

        [Test]
        public async Task Handle_ValidRequest_RecordsAssessment()
        {
            var admission = SeedAdmission();

            var response = await _handler.Handle(new RecordWeaningAssessmentRequestModel
            {
                HospitalId = admission.HospitalId,
                AdmissionId = admission.AdmissionId,
                SatPerformed = true,
                SatPassed = true,
                SbtPerformed = true,
                SbtPassed = false,
                LoggedInUserName = "Nurse Priya",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            var saved = _context.WeaningAssessment.Single(w => w.WeaningAssessmentId == response.WeaningAssessmentId);
            Assert.That(saved.SatPassed, Is.True);
            Assert.That(saved.SbtPassed, Is.False);
            Assert.That(saved.AssessedBy, Is.EqualTo("Nurse Priya"));
        }

        [Test]
        public async Task Handle_PassedWithoutPerformed_ForcesPassedFalse()
        {
            var admission = SeedAdmission();

            var response = await _handler.Handle(new RecordWeaningAssessmentRequestModel
            {
                HospitalId = admission.HospitalId,
                AdmissionId = admission.AdmissionId,
                SatPerformed = false,
                SatPassed = true,   // nonsensical: can't pass a trial never performed
                SbtPerformed = false,
                SbtPassed = true,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            var saved = _context.WeaningAssessment.Single(w => w.WeaningAssessmentId == response.WeaningAssessmentId);
            Assert.That(saved.SatPassed, Is.False);
            Assert.That(saved.SbtPassed, Is.False);
        }

        [Test]
        public async Task Handle_AdmissionNotFound_ReturnsError()
        {
            var response = await _handler.Handle(new RecordWeaningAssessmentRequestModel
            {
                HospitalId = Guid.NewGuid(),
                AdmissionId = Guid.NewGuid(),
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("not found"));
        }
    }
}
