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
    // Covers the Test Catalog "quick view" price projection -- Price isn't a column on
    // PathologyTestMaster, it's populated in-memory from the linked ChargeMaster's DefaultRate after
    // the main query (same batched-lookup shape GetPathologyOrdersHandler uses).
    [TestFixture]
    public class GetPathologyTestsQueryHandlerTests
    {
        private AppDbContext _context = null!;
        private GetPathologyTestsQueryHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetPathologyTestsQueryHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_TestWithLinkedCharge_ProjectsChargeMasterDefaultRateAsPrice()
        {
            var hospitalId = Guid.NewGuid();
            var chargeId = Guid.NewGuid();
            var testId = Guid.NewGuid();

            _context.ChargeMaster.Add(new ChargeMaster
            {
                ChargeId = chargeId,
                HospitalId = hospitalId,
                DisplayName = "Complete Blood Count",
                DefaultRate = 350m,
                IsActive = true,
            });
            _context.PathologyTestMaster.Add(new PathologyTestMaster
            {
                TestId = testId,
                HospitalId = hospitalId,
                TestCode = "HEM-CBC",
                TestName = "Complete Blood Count (CBC)",
                ChargeId = chargeId,
                IsActive = true,
            });
            _context.SaveChanges();

            var result = await _handler.Handle(new GetPathologyTestsQuery { HospitalId = hospitalId }, CancellationToken.None);

            Assert.That(result.Single(t => t.TestId == testId).Price, Is.EqualTo(350m));
        }

        [Test]
        public async Task Handle_TestWithNoLinkedCharge_PriceIsNull()
        {
            var hospitalId = Guid.NewGuid();
            var testId = Guid.NewGuid();
            _context.PathologyTestMaster.Add(new PathologyTestMaster
            {
                TestId = testId,
                HospitalId = hospitalId,
                TestCode = "HEM-ESR",
                TestName = "ESR",
                ChargeId = null,
                IsActive = true,
            });
            _context.SaveChanges();

            var result = await _handler.Handle(new GetPathologyTestsQuery { HospitalId = hospitalId }, CancellationToken.None);

            Assert.That(result.Single(t => t.TestId == testId).Price, Is.Null);
        }

        [Test]
        public async Task Handle_ChargeBelongsToAnotherHospital_PriceIsNull()
        {
            var hospitalId = Guid.NewGuid();
            var otherHospitalId = Guid.NewGuid();
            var chargeId = Guid.NewGuid();
            var testId = Guid.NewGuid();

            _context.ChargeMaster.Add(new ChargeMaster
            {
                ChargeId = chargeId,
                HospitalId = otherHospitalId,
                DisplayName = "Foreign Charge",
                DefaultRate = 999m,
                IsActive = true,
            });
            _context.PathologyTestMaster.Add(new PathologyTestMaster
            {
                TestId = testId,
                HospitalId = hospitalId,
                TestCode = "HEM-CBC",
                TestName = "Complete Blood Count (CBC)",
                ChargeId = chargeId,
                IsActive = true,
            });
            _context.SaveChanges();

            var result = await _handler.Handle(new GetPathologyTestsQuery { HospitalId = hospitalId }, CancellationToken.None);

            Assert.That(result.Single(t => t.TestId == testId).Price, Is.Null);
        }
    }
}
