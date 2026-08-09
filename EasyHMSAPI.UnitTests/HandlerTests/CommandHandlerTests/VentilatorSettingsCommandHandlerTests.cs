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
    public class VentilatorSettingsCommandHandlerTests
    {
        private AppDbContext _context = null!;
        private VentilatorSettingsCommandHandlers _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new VentilatorSettingsCommandHandlers(_context);
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
        public async Task Handle_ValidRequest_RecordsSettings()
        {
            var admission = SeedAdmission();

            var response = await _handler.Handle(new RecordVentilatorSettingsRequestModel
            {
                HospitalId = admission.HospitalId,
                AdmissionId = admission.AdmissionId,
                Mode = "simv",
                FiO2Percent = 40,
                PeepCmH2o = 5,
                LoggedInUserName = "Dr. Rao",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            var saved = _context.VentilatorSettings.Single(v => v.VentilatorSettingsId == response.VentilatorSettingsId);
            Assert.That(saved.Mode, Is.EqualTo("SIMV"));
            Assert.That(saved.FiO2Percent, Is.EqualTo(40));
            Assert.That(saved.ScoredBy, Is.EqualTo("Dr. Rao"));
        }

        [Test]
        public async Task Handle_MissingMode_ReturnsError()
        {
            var admission = SeedAdmission();

            var response = await _handler.Handle(new RecordVentilatorSettingsRequestModel
            {
                HospitalId = admission.HospitalId,
                AdmissionId = admission.AdmissionId,
                Mode = "",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("mode"));
        }

        [Test]
        public async Task Handle_AdmissionNotFound_ReturnsError()
        {
            var response = await _handler.Handle(new RecordVentilatorSettingsRequestModel
            {
                HospitalId = Guid.NewGuid(),
                AdmissionId = Guid.NewGuid(),
                Mode = "AC",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("not found"));
        }
    }
}
