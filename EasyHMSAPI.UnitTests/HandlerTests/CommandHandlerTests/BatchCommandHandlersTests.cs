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
    public class BatchCommandHandlersTests
    {
        private AppDbContext _context = null!;
        private BatchCommandHandlers _handler = null!;
        private Guid _hospitalId;
        private Guid _storeId;
        private Guid _inventoryItemId;

        [SetUp]
        public async Task SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new BatchCommandHandlers(_context);
            _hospitalId = Guid.NewGuid();
            _storeId = Guid.NewGuid();
            _inventoryItemId = Guid.NewGuid();

            _context.Store.Add(new Store
            {
                StoreId = _storeId,
                HospitalId = _hospitalId,
                StoreCode = "MAIN",
                StoreName = "Main Store",
                StoreType = "MAIN",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            _context.InventoryItem.Add(new InventoryItem
            {
                InventoryItemId = _inventoryItemId,
                HospitalId = _hospitalId,
                ItemCode = "PARA",
                ItemName = "Paracetamol",
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
        }

        [TearDown]
        public void TearDown()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        private CreateBatchRequestModel ValidRequest(string batchNumber, DateTime? expiryDate, decimal qty = 10) => new()
        {
            HospitalId = _hospitalId,
            InventoryItemId = _inventoryItemId,
            StoreId = _storeId,
            BatchNumber = batchNumber,
            ExpiryDate = expiryDate,
            ReceivedQty = qty,
        };

        [Test]
        public async Task Handle_NewBatch_CreatesBatchWithZeroRemainingQty()
        {
            var response = await _handler.Handle(ValidRequest("B-001", new DateTime(2027, 6, 30)), CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.BatchId, Is.Not.Null);
            var batch = _context.Batch.Single();
            Assert.That(batch.RemainingQty, Is.EqualTo(0), "RemainingQty starts at 0 — the caller's own RECEIVE movement brings it up");
        }

        [Test]
        public async Task Handle_SameItemStoreBatchNumberAndExpiry_ReturnsExistingBatchIdInsteadOfCreatingDuplicate()
        {
            var expiry = new DateTime(2027, 6, 30);
            var first = await _handler.Handle(ValidRequest("B-001", expiry), CancellationToken.None);
            Assert.That(first.Success, Is.True);

            var second = await _handler.Handle(ValidRequest("B-001", expiry, qty: 5), CancellationToken.None);

            Assert.That(second.Success, Is.True);
            Assert.That(second.BatchId, Is.EqualTo(first.BatchId));
            Assert.That(_context.Batch.Count(), Is.EqualTo(1));
        }

        [Test]
        public async Task Handle_SameBatchNumberDifferentExpiry_CreatesSeparateBatch()
        {
            var first = await _handler.Handle(ValidRequest("B-001", new DateTime(2027, 6, 30)), CancellationToken.None);
            var second = await _handler.Handle(ValidRequest("B-001", new DateTime(2027, 12, 31)), CancellationToken.None);

            Assert.That(first.Success, Is.True);
            Assert.That(second.Success, Is.True);
            Assert.That(second.BatchId, Is.Not.EqualTo(first.BatchId));
            Assert.That(_context.Batch.Count(), Is.EqualTo(2));
        }

        [Test]
        public async Task Handle_ItemNotFound_ReturnsError()
        {
            var request = ValidRequest("B-001", null);
            request.InventoryItemId = Guid.NewGuid();

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("Inventory item not found"));
        }
    }
}
