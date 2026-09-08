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
    public class GetReorderThresholdSuggestionsHandlerTests
    {
        private AppDbContext _context = null!;
        private GetReorderThresholdSuggestionsHandler _handler = null!;
        private Guid _hospitalId;
        private Guid _storeId;
        private Guid _itemId;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetReorderThresholdSuggestionsHandler(_context);
            _hospitalId = Guid.NewGuid();
            _storeId = Guid.NewGuid();
            _itemId = Guid.NewGuid();

            _context.InventoryItem.Add(new InventoryItem
            {
                InventoryItemId = _itemId,
                HospitalId = _hospitalId,
                ItemCode = "PARA",
                ItemName = "Paracetamol",
                Category = "DRUG",
                Unit = "TAB",
                CurrentStock = 5,
                MinStockLevel = 2,
                MaxStockLevel = 10,
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

        private void SeedIssue(decimal qty, int daysAgo, Guid? storeId = null)
        {
            _context.InventoryMovement.Add(new InventoryMovement
            {
                InventoryMovementId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                InventoryItemId = _itemId,
                MovementType = "ISSUE",
                Qty = qty,
                FromStoreId = storeId ?? _storeId,
                MovedAt = DateTime.UtcNow.AddDays(-daysAgo),
                CreatedAt = DateTime.UtcNow,
            });
        }

        [Test]
        public async Task Handle_NoMovementHistory_ReturnsNoSuggestions()
        {
            var response = await _handler.Handle(new GetReorderThresholdSuggestionsRequestModel { HospitalId = _hospitalId }, CancellationToken.None);
            Assert.That(response.Suggestions, Is.Empty);
        }

        [Test]
        public async Task Handle_TrailingIssues_ComputesWeeklyAverageAndSuggestedThresholds()
        {
            // 28 units issued across the trailing 28-day window -> 7/week average.
            SeedIssue(10, daysAgo: 5);
            SeedIssue(10, daysAgo: 12);
            SeedIssue(8, daysAgo: 20);
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetReorderThresholdSuggestionsRequestModel { HospitalId = _hospitalId, BufferMultiplier = 1.5m }, CancellationToken.None);

            Assert.That(response.Suggestions, Has.Count.EqualTo(1));
            var s = response.Suggestions[0];
            Assert.That(s.Trailing4WeekIssuedQty, Is.EqualTo(28));
            Assert.That(s.WeeklyAverageConsumption, Is.EqualTo(7));
            Assert.That(s.SuggestedMinStockLevel, Is.EqualTo(10.5m));
            Assert.That(s.SuggestedMaxStockLevel, Is.EqualTo(31.5m));
            Assert.That(s.IsBelowSuggestedMin, Is.True); // CurrentStock=5 < 10.5
        }

        [Test]
        public async Task Handle_IssueOutsideTrailingWindow_IsExcluded()
        {
            SeedIssue(50, daysAgo: 60); // outside the 28-day window
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetReorderThresholdSuggestionsRequestModel { HospitalId = _hospitalId }, CancellationToken.None);
            Assert.That(response.Suggestions, Is.Empty);
        }

        [Test]
        public async Task Handle_StoreFilter_ScopesConsumptionToThatStoreOnly()
        {
            var otherStore = Guid.NewGuid();
            SeedIssue(10, daysAgo: 3, storeId: _storeId);
            SeedIssue(100, daysAgo: 3, storeId: otherStore);
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetReorderThresholdSuggestionsRequestModel { HospitalId = _hospitalId, StoreId = _storeId }, CancellationToken.None);

            Assert.That(response.Suggestions, Has.Count.EqualTo(1));
            Assert.That(response.Suggestions[0].Trailing4WeekIssuedQty, Is.EqualTo(10));
        }

        [Test]
        public async Task Handle_ReceiveMovements_AreNotCountedAsConsumption()
        {
            _context.InventoryMovement.Add(new InventoryMovement
            {
                InventoryMovementId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                InventoryItemId = _itemId,
                MovementType = "RECEIVE",
                Qty = 100,
                ToStoreId = _storeId,
                MovedAt = DateTime.UtcNow.AddDays(-3),
                CreatedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetReorderThresholdSuggestionsRequestModel { HospitalId = _hospitalId }, CancellationToken.None);
            Assert.That(response.Suggestions, Is.Empty);
        }
    }
}
