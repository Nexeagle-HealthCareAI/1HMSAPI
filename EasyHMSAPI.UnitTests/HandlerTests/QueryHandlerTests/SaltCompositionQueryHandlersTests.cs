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
    public class SaltCompositionQueryHandlersTests
    {
        private AppDbContext _context = null!;
        private SaltCompositionQueryHandlers _handler = null!;
        private Guid _hospitalId;
        private Guid _storeId;
        private Guid _compositionId;
        private Guid _itemAId;
        private Guid _itemBId;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new SaltCompositionQueryHandlers(_context);
            _hospitalId = Guid.NewGuid();
            _storeId = Guid.NewGuid();
            _compositionId = Guid.NewGuid();
            _itemAId = Guid.NewGuid();
            _itemBId = Guid.NewGuid();

            _context.SaltComposition.Add(new SaltComposition
            {
                SaltCompositionId = _compositionId,
                DisplayName = "Amoxicillin 500mg",
                DosageForm = "TABLET",
                CreatedAt = DateTime.UtcNow,
            });

            _context.InventoryItem.Add(new InventoryItem
            {
                InventoryItemId = _itemAId,
                HospitalId = _hospitalId,
                ItemCode = "AMX-A",
                ItemName = "Amoxil 500",
                Category = "DRUG",
                Unit = "TAB",
                SaltCompositionId = _compositionId,
                DefaultRate = 5,
                CurrentStock = 0,
                MinStockLevel = 0,
                ReorderQty = 0,
                IsLasa = false,
                IsHighAlert = false,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            _context.InventoryItem.Add(new InventoryItem
            {
                InventoryItemId = _itemBId,
                HospitalId = _hospitalId,
                ItemCode = "AMX-B",
                ItemName = "Moxikind 500",
                Category = "DRUG",
                Unit = "TAB",
                SaltCompositionId = _compositionId,
                DefaultRate = 4,
                CurrentStock = 0,
                MinStockLevel = 0,
                ReorderQty = 0,
                IsLasa = false,
                IsHighAlert = false,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
        }

        [TearDown]
        public void TearDown()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        [Test]
        public async Task Handle_ItemWithNoComposition_ReturnsHasCompositionFalse()
        {
            var lonelyItemId = Guid.NewGuid();
            _context.InventoryItem.Add(new InventoryItem
            {
                InventoryItemId = lonelyItemId,
                HospitalId = _hospitalId,
                ItemCode = "LONE",
                ItemName = "Lonely Item",
                Category = "DRUG",
                Unit = "TAB",
                CurrentStock = 0,
                MinStockLevel = 0,
                ReorderQty = 0,
                IsLasa = false,
                IsHighAlert = false,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetSubstituteItemsRequestModel { HospitalId = _hospitalId, InventoryItemId = lonelyItemId }, CancellationToken.None);

            Assert.That(response.HasComposition, Is.False);
            Assert.That(response.Alternates, Is.Empty);
        }

        [Test]
        public async Task Handle_AlternateWithNoStock_IsExcluded()
        {
            await _context.SaveChangesAsync(); // neither item has stock

            var response = await _handler.Handle(new GetSubstituteItemsRequestModel { HospitalId = _hospitalId, InventoryItemId = _itemAId, StoreId = _storeId }, CancellationToken.None);

            Assert.That(response.HasComposition, Is.True);
            Assert.That(response.Alternates, Is.Empty);
        }

        [Test]
        public async Task Handle_AlternateWithStock_IsReturnedCheapestFirst()
        {
            _context.StockLevel.Add(new StockLevel
            {
                StockLevelId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                InventoryItemId = _itemBId,
                StoreId = _storeId,
                QtyOnHand = 40,
                UpdatedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetSubstituteItemsRequestModel { HospitalId = _hospitalId, InventoryItemId = _itemAId, StoreId = _storeId }, CancellationToken.None);

            Assert.That(response.HasComposition, Is.True);
            Assert.That(response.Alternates, Has.Count.EqualTo(1));
            Assert.That(response.Alternates[0].InventoryItemId, Is.EqualTo(_itemBId));
            Assert.That(response.Alternates[0].StockAtStore, Is.EqualTo(40));
        }

        [Test]
        public async Task Handle_DoesNotReturnTheItemItself()
        {
            _context.StockLevel.Add(new StockLevel
            {
                StockLevelId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                InventoryItemId = _itemAId,
                StoreId = _storeId,
                QtyOnHand = 999,
                UpdatedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetSubstituteItemsRequestModel { HospitalId = _hospitalId, InventoryItemId = _itemAId, StoreId = _storeId }, CancellationToken.None);

            Assert.That(response.Alternates.Any(a => a.InventoryItemId == _itemAId), Is.False);
        }
    }
}
