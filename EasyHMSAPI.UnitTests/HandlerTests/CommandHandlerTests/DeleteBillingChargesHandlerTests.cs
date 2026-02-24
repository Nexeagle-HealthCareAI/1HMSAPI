using System;
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
    public class DeleteBillingChargesHandlerTests
    {
        private AppDbContext _context = null!;
        private DeleteBillingChargesHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new DeleteBillingChargesHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ValidItem_DeletesSuccessfully()
        {
            // Arrange
            var hospitalId = Guid.NewGuid();
            var item = new BillingChargeCatalog
            {
                ChargeItemId = Guid.NewGuid(),
                HospitalId = hospitalId,
                DisplayName = "Test Service",
                DefaultRate = 100
            };
            _context.BillingChargeCatalog.Add(item);
            await _context.SaveChangesAsync();

            var request = new DeleteBillingChargesRequestModel 
            { 
                ChargeItemId = item.ChargeItemId, 
                HospitalId = hospitalId 
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            var deletedItem = await _context.BillingChargeCatalog.FindAsync(item.ChargeItemId);
            Assert.That(deletedItem, Is.Null);
        }

        [Test]
        public async Task Handle_ItemNotFound_ReturnsFailure()
        {
            // Arrange
            var request = new DeleteBillingChargesRequestModel 
            { 
                ChargeItemId = Guid.NewGuid(), 
                HospitalId = Guid.NewGuid()
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Billing charge item not found."));
        }
    }
}
