using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class EarlyWarningScoreCommandHandlerTests
    {
        private AppDbContext _context = null!;
        private EarlyWarningScoreCommandHandlers _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new EarlyWarningScoreCommandHandlers(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private (Guid hospitalId, Admission admission) SeedBasics()
        {
            var hospitalId = Guid.NewGuid();
            _context.Hospitals.Add(new Hospital
            {
                HospitalID = hospitalId, Name = "Hosp", Email = "e@m.com", Type = "General", RegistrationNumber = "REG001",
                Contact = "1234567890", Location = "Test Location", City = "Test City", State = "Test State",
                Country = "Test Country", Pincode = "123456", CreatedByUserID = Guid.NewGuid(),
            });
            var admission = new Admission
            {
                AdmissionId = Guid.NewGuid(), HospitalId = hospitalId, PatientId = "PTID00000001",
                AdmissionNo = "ADM-1", AdmittedAt = DateTime.UtcNow, StatusCode = "ADMITTED",
            };
            _context.Admission.Add(admission);
            _context.SaveChanges();
            return (hospitalId, admission);
        }

        [Test]
        public async Task Handle_AllNormalVitals_ReturnsLowRiskZeroScore()
        {
            var (hospitalId, admission) = SeedBasics();

            var response = await _handler.Handle(new RecordEarlyWarningScoreRequestModel
            {
                HospitalId = hospitalId, AdmissionId = admission.AdmissionId,
                RespiratoryRate = 16, Spo2 = 98m, SupplementalOxygen = false, SystolicBp = 120,
                Pulse = 75, ConsciousnessLevel = IpdConstants.EwsConsciousnessLevel.Alert, TemperatureC = 37.0m,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            Assert.That(response.TotalScore, Is.EqualTo(0));
            Assert.That(response.RiskBand, Is.EqualTo(IpdConstants.EwsRiskBand.Low));
            Assert.That(response.EscalationRecommended, Is.False);
        }

        [Test]
        public async Task Handle_SeverelyAbnormalVitals_ReturnsHighRiskAndEscalationRecommended()
        {
            var (hospitalId, admission) = SeedBasics();

            var response = await _handler.Handle(new RecordEarlyWarningScoreRequestModel
            {
                HospitalId = hospitalId, AdmissionId = admission.AdmissionId,
                RespiratoryRate = 30, Spo2 = 85m, SupplementalOxygen = true, SystolicBp = 80,
                Pulse = 140, ConsciousnessLevel = IpdConstants.EwsConsciousnessLevel.Pain, TemperatureC = 34.0m,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            Assert.That(response.RiskBand, Is.EqualTo(IpdConstants.EwsRiskBand.High));
            Assert.That(response.EscalationRecommended, Is.True);

            var saved = _context.EarlyWarningScore.Single(s => s.ScoreId == response.ScoreId);
            Assert.That(saved.AdmissionId, Is.EqualTo(admission.AdmissionId));
            Assert.That(saved.ConsciousnessLevel, Is.EqualTo(IpdConstants.EwsConsciousnessLevel.Pain));
        }

        [Test]
        public async Task Handle_InvalidConsciousnessLevel_ReturnsFailure()
        {
            var (hospitalId, admission) = SeedBasics();

            var response = await _handler.Handle(new RecordEarlyWarningScoreRequestModel
            {
                HospitalId = hospitalId, AdmissionId = admission.AdmissionId, ConsciousnessLevel = "NOT_A_REAL_LEVEL",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("Invalid"));
        }

        [Test]
        public async Task Handle_UnknownAdmission_ReturnsFailure()
        {
            var (hospitalId, _) = SeedBasics();

            var response = await _handler.Handle(new RecordEarlyWarningScoreRequestModel
            {
                HospitalId = hospitalId, AdmissionId = Guid.NewGuid(),
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("not found"));
        }
    }
}
