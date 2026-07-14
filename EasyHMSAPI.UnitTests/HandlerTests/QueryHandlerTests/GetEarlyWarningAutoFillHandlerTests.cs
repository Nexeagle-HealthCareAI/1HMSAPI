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
    public class GetEarlyWarningAutoFillHandlerTests
    {
        private AppDbContext _context = null!;
        private GetEarlyWarningAutoFillHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetEarlyWarningAutoFillHandler(_context);
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
        public async Task Handle_PullsLatestVitalReading_InCelsius()
        {
            var (hospitalId, admission) = SeedBasics();
            _context.VitalReading.Add(new VitalReading
            {
                VitalReadingId = Guid.NewGuid(), HospitalId = hospitalId, AdmissionId = admission.AdmissionId,
                RecordedAt = DateTime.UtcNow.AddMinutes(-5), RespiratoryRate = 18, SpO2 = 96m, SystolicBP = 118, Pulse = 80,
                Temperature = 37.2m, TemperatureUnit = "C",
            });
            await _context.SaveChangesAsync();

            var result = await _handler.Handle(new GetEarlyWarningAutoFillRequestModel { HospitalId = hospitalId, AdmissionId = admission.AdmissionId }, CancellationToken.None);

            Assert.That(result.RespiratoryRate, Is.EqualTo(18));
            Assert.That(result.Spo2, Is.EqualTo(96m));
            Assert.That(result.SystolicBp, Is.EqualTo(118));
            Assert.That(result.Pulse, Is.EqualTo(80));
            Assert.That(result.TemperatureC, Is.EqualTo(37.2m));
            Assert.That(result.SourceVitalRecordedAt, Is.Not.Null);
        }

        [Test]
        public async Task Handle_FahrenheitReading_ConvertsToCelsius()
        {
            var (hospitalId, admission) = SeedBasics();
            _context.VitalReading.Add(new VitalReading
            {
                VitalReadingId = Guid.NewGuid(), HospitalId = hospitalId, AdmissionId = admission.AdmissionId,
                RecordedAt = DateTime.UtcNow, Temperature = 98.6m, TemperatureUnit = "F",
            });
            await _context.SaveChangesAsync();

            var result = await _handler.Handle(new GetEarlyWarningAutoFillRequestModel { HospitalId = hospitalId, AdmissionId = admission.AdmissionId }, CancellationToken.None);

            Assert.That(result.TemperatureC, Is.EqualTo(37.0m));
        }

        [Test]
        public async Task Handle_NoVitalsRecorded_ReturnsAllNulls()
        {
            var (hospitalId, admission) = SeedBasics();

            var result = await _handler.Handle(new GetEarlyWarningAutoFillRequestModel { HospitalId = hospitalId, AdmissionId = admission.AdmissionId }, CancellationToken.None);

            Assert.That(result.RespiratoryRate, Is.Null);
            Assert.That(result.SourceVitalRecordedAt, Is.Null);
        }
    }
}
