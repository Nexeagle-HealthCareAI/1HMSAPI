using System;
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
    public class GetEarlyWarningScoreHistoryHandlerTests
    {
        private AppDbContext _context = null!;
        private GetEarlyWarningScoreHistoryHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetEarlyWarningScoreHistoryHandler(_context);
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

        private void SeedScore(Guid hospitalId, Guid admissionId, int totalScore, DateTime scoredAt)
        {
            _context.EarlyWarningScore.Add(new EarlyWarningScore
            {
                ScoreId = Guid.NewGuid(), HospitalId = hospitalId, AdmissionId = admissionId,
                TotalScore = totalScore, RiskBand = "LOW", ScoredBy = "Nurse Joy", ScoredAt = scoredAt,
            });
        }

        [Test]
        public async Task Handle_ReturnsScores_NewestFirst()
        {
            var (hospitalId, admission) = SeedBasics();
            SeedScore(hospitalId, admission.AdmissionId, 2, DateTime.UtcNow.AddHours(-2));
            SeedScore(hospitalId, admission.AdmissionId, 5, DateTime.UtcNow);
            await _context.SaveChangesAsync();

            var result = await _handler.Handle(new GetEarlyWarningScoreHistoryRequestModel { HospitalId = hospitalId, AdmissionId = admission.AdmissionId }, CancellationToken.None);

            Assert.That(result.Scores, Has.Count.EqualTo(2));
            Assert.That(result.Scores[0].TotalScore, Is.EqualTo(5));
            Assert.That(result.Scores[1].TotalScore, Is.EqualTo(2));
        }

        [Test]
        public async Task Handle_NoScores_ReturnsEmptyList()
        {
            var (hospitalId, admission) = SeedBasics();

            var result = await _handler.Handle(new GetEarlyWarningScoreHistoryRequestModel { HospitalId = hospitalId, AdmissionId = admission.AdmissionId }, CancellationToken.None);

            Assert.That(result.Scores, Is.Empty);
        }
    }
}
