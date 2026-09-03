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
    }
}
