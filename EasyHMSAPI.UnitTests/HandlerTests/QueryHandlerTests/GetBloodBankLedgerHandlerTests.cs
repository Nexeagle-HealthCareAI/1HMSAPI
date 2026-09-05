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
    public class GetBloodBankLedgerHandlerTests
    {
        private AppDbContext _context = null!;
        private GetBloodBankLedgerHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetBloodBankLedgerHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ReturnsHospitalWideTransfusionsNewestFirst_WithPatientName()
        {
            var hospitalId = Guid.NewGuid();
            var bagId1 = Guid.NewGuid();
            var bagId2 = Guid.NewGuid();
            _context.PatientRegistrations.Add(new PatientRegistration { RegistrationId = Guid.NewGuid(), HospitalId = hospitalId, PatientId = "PTID001", FullName = "Jane Doe" });
            _context.BloodBag.Add(new BloodBag { BloodBagId = bagId1, HospitalId = hospitalId, BagNumber = "BB-1", Component = "PRBC", BloodGroup = "O_POS", VolumeMl = 350, CollectedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddDays(30), Status = "TRANSFUSED" });
            _context.BloodBag.Add(new BloodBag { BloodBagId = bagId2, HospitalId = hospitalId, BagNumber = "BB-2", Component = "FFP", BloodGroup = "A_POS", VolumeMl = 200, CollectedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddDays(30), Status = "TRANSFUSED" });

            _context.TransfusionEvent.Add(new TransfusionEvent
            {
                TransfusionEventId = Guid.NewGuid(), HospitalId = hospitalId, BloodBagId = bagId1, AdmissionId = Guid.NewGuid(),
                PatientId = "PTID001", StartedAt = DateTime.UtcNow.AddHours(-2), VolumeGivenMl = 300, Reaction = "NONE",
                AdministeredBy = "Nurse A", WitnessName = "Nurse B",
            });
            _context.TransfusionEvent.Add(new TransfusionEvent
            {
                TransfusionEventId = Guid.NewGuid(), HospitalId = hospitalId, BloodBagId = bagId2, AdmissionId = Guid.NewGuid(),
                PatientId = "PTID999", StartedAt = DateTime.UtcNow, VolumeGivenMl = 180, Reaction = "MILD",
                AdministeredBy = "Nurse C", WitnessName = "Nurse D",
            });
            _context.SaveChanges();

            var response = await _handler.Handle(new GetBloodBankLedgerRequestModel { HospitalId = hospitalId }, CancellationToken.None);

            Assert.That(response.Transfusions.Count, Is.EqualTo(2));
            Assert.That(response.Transfusions[0].BagNumber, Is.EqualTo("BB-2"), "newest (most recent StartedAt) first");
            var janesRow = response.Transfusions.Find(t => t.BagNumber == "BB-1");
            Assert.That(janesRow!.PatientName, Is.EqualTo("Jane Doe"));
        }
    }
}
