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
    public class GetBloodBankInventoryHandlerTests
    {
        private AppDbContext _context = null!;
        private GetBloodBankInventoryHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetBloodBankInventoryHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ReturnsAllBagsRegardlessOfStatus_WithReservedPatientName()
        {
            var hospitalId = Guid.NewGuid();
            _context.PatientRegistrations.Add(new PatientRegistration { RegistrationId = Guid.NewGuid(), HospitalId = hospitalId, PatientId = "PTID001", FullName = "Jane Doe" });
            _context.BloodBag.Add(new BloodBag
            {
                BloodBagId = Guid.NewGuid(), HospitalId = hospitalId, BagNumber = "BB-1", Component = "PRBC", BloodGroup = "O_POS",
                VolumeMl = 350, CollectedAt = DateTime.UtcNow.AddDays(-5), ExpiresAt = DateTime.UtcNow.AddDays(30), Status = "AVAILABLE",
            });
            _context.BloodBag.Add(new BloodBag
            {
                BloodBagId = Guid.NewGuid(), HospitalId = hospitalId, BagNumber = "BB-2", Component = "PRBC", BloodGroup = "A_POS",
                VolumeMl = 350, CollectedAt = DateTime.UtcNow.AddDays(-5), ExpiresAt = DateTime.UtcNow.AddDays(30), Status = "RESERVED",
                ReservedForPatientId = "PTID001",
            });
            _context.BloodBag.Add(new BloodBag
            {
                BloodBagId = Guid.NewGuid(), HospitalId = hospitalId, BagNumber = "BB-3", Component = "FFP", BloodGroup = "B_NEG",
                VolumeMl = 200, CollectedAt = DateTime.UtcNow.AddDays(-40), ExpiresAt = DateTime.UtcNow.AddDays(-5), Status = "DISCARDED",
                DiscardReason = "Expired",
            });
            _context.SaveChanges();

            var response = await _handler.Handle(new GetBloodBankInventoryRequestModel { HospitalId = hospitalId }, CancellationToken.None);

            Assert.That(response.Bags.Count, Is.EqualTo(3));
            var reserved = response.Bags.Single(b => b.BagNumber == "BB-2");
            Assert.That(reserved.ReservedForPatientName, Is.EqualTo("Jane Doe"));
        }

        [Test]
        public async Task Handle_StatusFilter_ReturnsOnlyMatchingBags()
        {
            var hospitalId = Guid.NewGuid();
            _context.BloodBag.Add(new BloodBag { BloodBagId = Guid.NewGuid(), HospitalId = hospitalId, BagNumber = "BB-1", Component = "PRBC", BloodGroup = "O_POS", VolumeMl = 350, CollectedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddDays(30), Status = "AVAILABLE" });
            _context.BloodBag.Add(new BloodBag { BloodBagId = Guid.NewGuid(), HospitalId = hospitalId, BagNumber = "BB-2", Component = "PRBC", BloodGroup = "O_POS", VolumeMl = 350, CollectedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddDays(30), Status = "DISCARDED" });
            _context.SaveChanges();

            var response = await _handler.Handle(new GetBloodBankInventoryRequestModel { HospitalId = hospitalId, Status = "AVAILABLE" }, CancellationToken.None);

            Assert.That(response.Bags.Count, Is.EqualTo(1));
            Assert.That(response.Bags[0].BagNumber, Is.EqualTo("BB-1"));
        }
    }
}
