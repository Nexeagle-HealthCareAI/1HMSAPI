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
    public class GetBillingChargesHandlerTests
    {
        private AppDbContext _context = null!;
        private GetBillingChargesHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetBillingChargesHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ReturnsCharges()
        {
            // Arrange
            var hospitalId = Guid.NewGuid();
            var charge = new BillingChargeCatalog 
            { 
                ChargeItemId = Guid.NewGuid(), 
                HospitalId = hospitalId, 
                DisplayName = "Consultation",
                DefaultRate = 500
            };
            _context.BillingChargeCatalog.Add(charge);
            await _context.SaveChangesAsync();

            var request = new GetBillingChargesRequestModel { HospitalId = hospitalId };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.Data, Has.Count.EqualTo(1));
            Assert.That(response.Data[0].DisplayName, Is.EqualTo("Consultation"));
        }
    }
}
