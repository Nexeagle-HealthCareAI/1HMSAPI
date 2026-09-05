using System;
using System.Collections.Generic;
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
    public class BulkBatchCommandHandlersTests
    {
        private AppDbContext _context = null!;
        private BulkBatchCommandHandlers _handler = null!;
        private Guid _hospitalId;

        [SetUp]
        public async Task SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new BulkBatchCommandHandlers(_context);
            _hospitalId = Guid.NewGuid();

            _context.Store.Add(new Store
            {
                StoreId = Guid.NewGuid(),
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
                InventoryItemId = Guid.NewGuid(),
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

        private static CreateBulkBatchRequestModel ValidRequest(Guid hospitalId, List<BulkBatchRowModel> rows) => new()
        {
            HospitalId = hospitalId,
            Rows = rows,
        };

        [Test]
        public async Task Handle_ValidRow_CreatesBatchAndUpdatesCurrentStockAndMovement()
        {
            var request = ValidRequest(_hospitalId, new List<BulkBatchRowModel>
            {
                new() { StoreCode = "MAIN", ItemCode = "PARA", BatchNumber = "B-001", ReceivedQty = 25, UnitCost = 1.8m, Mrp = 2.5m }
            });

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.SuccessCount, Is.EqualTo(1));
            Assert.That(_context.Batch.Count(), Is.EqualTo(1));
            Assert.That(_context.StockLevel.Single().QtyOnHand, Is.EqualTo(25));
            Assert.That(_context.InventoryItem.Single().CurrentStock, Is.EqualTo(25));
            var movement = _context.InventoryMovement.Single();
            Assert.That(movement.MovementType, Is.EqualTo("RECEIVE"));
            Assert.That(movement.Qty, Is.EqualTo(25));
            Assert.That(movement.SourceModule, Is.EqualTo("BULK_IMPORT"));
        }

        [Test]
        public async Task Handle_UnknownItemCode_ReportsRowError()
        {
            var request = ValidRequest(_hospitalId, new List<BulkBatchRowModel>
            {
                new() { StoreCode = "MAIN", ItemCode = "GHOST", BatchNumber = "B-001", ReceivedQty = 10 }
            });

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.SuccessCount, Is.EqualTo(0));
            Assert.That(response.Errors, Has.Count.EqualTo(1));
            Assert.That(response.Errors[0].ErrorMessage, Does.Contain("GHOST"));
        }

        [Test]
        public async Task Handle_MixOfValidAndInvalidRows_ProcessesValidAndReportsInvalid()
        {
            var request = ValidRequest(_hospitalId, new List<BulkBatchRowModel>
            {
                new() { StoreCode = "MAIN", ItemCode = "PARA", BatchNumber = "B-001", ReceivedQty = 10 },
                new() { StoreCode = "MAIN", ItemCode = "PARA", BatchNumber = "B-002", ReceivedQty = 0 }, // invalid qty
            });

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.SuccessCount, Is.EqualTo(1));
            Assert.That(response.Errors, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task Handle_NoRows_ReturnsError()
        {
            var response = await _handler.Handle(ValidRequest(_hospitalId, new List<BulkBatchRowModel>()), CancellationToken.None);
            Assert.That(response.Success, Is.False);
        }

        [Test]
        public async Task Handle_SameBatchAndExpiry_MergesIntoExistingBatchInsteadOfDuplicating()
        {
            var store = _context.Store.Single();
            var item = _context.InventoryItem.Single();
            var expiry = new DateTime(2027, 6, 30);
            var existingBatch = new Batch
            {
                BatchId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                InventoryItemId = item.InventoryItemId,
                StoreId = store.StoreId,
                BatchNumber = "B-001",
                ExpiryDate = expiry,
                ReceivedQty = 20,
                RemainingQty = 15,
                Status = "ACTIVE",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            _context.Batch.Add(existingBatch);
            _context.StockLevel.Add(new StockLevel
            {
                StockLevelId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                InventoryItemId = item.InventoryItemId,
                StoreId = store.StoreId,
                QtyOnHand = 15,
                UpdatedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();

            var request = ValidRequest(_hospitalId, new List<BulkBatchRowModel>
            {
                new() { StoreCode = "MAIN", ItemCode = "PARA", BatchNumber = "B-001", ExpiryDate = expiry, ReceivedQty = 10 }
            });

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.SuccessCount, Is.EqualTo(1));
            Assert.That(_context.Batch.Count(), Is.EqualTo(1), "must not create a second Batch row for the same item+store+batch+expiry");
            var mergedBatch = _context.Batch.Single();
            Assert.That(mergedBatch.BatchId, Is.EqualTo(existingBatch.BatchId));
            Assert.That(mergedBatch.ReceivedQty, Is.EqualTo(30));
            Assert.That(mergedBatch.RemainingQty, Is.EqualTo(25));
            Assert.That(_context.StockLevel.Single().QtyOnHand, Is.EqualTo(25), "stock level must only increase by this row's qty, not the batch's new running total");
            var movement = _context.InventoryMovement.Single();
            Assert.That(movement.Qty, Is.EqualTo(10));
            Assert.That(movement.BatchId, Is.EqualTo(existingBatch.BatchId));
        }

        [Test]
        public async Task Handle_UnknownItemCodeWithItemName_AutoCreatesMedicineAndBatch()
        {
            var request = ValidRequest(_hospitalId, new List<BulkBatchRowModel>
            {
                new() { StoreCode = "MAIN", ItemCode = "NEWMED", ItemName = "Brand New Tablet", BatchNumber = "B-500", ReceivedQty = 40, Mrp = 15m }
            });

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            Assert.That(response.SuccessCount, Is.EqualTo(1));
            Assert.That(response.CreatedItemCount, Is.EqualTo(1));
            var created = _context.InventoryItem.Single(i => i.ItemCode == "NEWMED");
            Assert.That(created.ItemName, Is.EqualTo("Brand New Tablet"));
            Assert.That(created.Category, Is.EqualTo("DRUG"));
            Assert.That(created.CurrentStock, Is.EqualTo(40));
            Assert.That(_context.Batch.Any(b => b.InventoryItemId == created.InventoryItemId && b.BatchNumber == "B-500"), Is.True);
        }

        [Test]
        public async Task Handle_TwoRowsSameNewItemCode_CreatesOnlyOneMedicine()
        {
            var request = ValidRequest(_hospitalId, new List<BulkBatchRowModel>
            {
                new() { StoreCode = "MAIN", ItemCode = "NEWMED", ItemName = "Brand New Tablet", BatchNumber = "B-500", ReceivedQty = 40 },
                new() { StoreCode = "MAIN", ItemCode = "NEWMED", ItemName = "Brand New Tablet", BatchNumber = "B-501", ReceivedQty = 10 },
            });

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            Assert.That(response.SuccessCount, Is.EqualTo(2));
            Assert.That(response.CreatedItemCount, Is.EqualTo(1));
            Assert.That(_context.InventoryItem.Count(i => i.ItemCode == "NEWMED"), Is.EqualTo(1));
            var created = _context.InventoryItem.Single(i => i.ItemCode == "NEWMED");
            Assert.That(created.CurrentStock, Is.EqualTo(50));
        }

        [Test]
        public async Task Handle_UnknownItemCodeWithoutItemName_StillReportsRowError()
        {
            var request = ValidRequest(_hospitalId, new List<BulkBatchRowModel>
            {
                new() { StoreCode = "MAIN", ItemCode = "GHOST", BatchNumber = "B-001", ReceivedQty = 10 }
            });

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Errors[0].ErrorMessage, Does.Contain("Item Name"));
        }

        [Test]
        public async Task Handle_SameBatchNumberDifferentExpiry_CreatesSeparateBatch()
        {
            var store = _context.Store.Single();
            var item = _context.InventoryItem.Single();
            _context.Batch.Add(new Batch
            {
                BatchId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                InventoryItemId = item.InventoryItemId,
                StoreId = store.StoreId,
                BatchNumber = "B-001",
                ExpiryDate = new DateTime(2027, 6, 30),
                ReceivedQty = 20,
                RemainingQty = 20,
                Status = "ACTIVE",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();

            var request = ValidRequest(_hospitalId, new List<BulkBatchRowModel>
            {
                // Same batch number, different expiry — likely a distinct lot (or a typo), must
                // stay a separate batch rather than silently merging mismatched expiries.
                new() { StoreCode = "MAIN", ItemCode = "PARA", BatchNumber = "B-001", ExpiryDate = new DateTime(2027, 12, 31), ReceivedQty = 10 }
            });

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(_context.Batch.Count(), Is.EqualTo(2));
        }
    }
}
