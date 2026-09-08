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
    public class GetBatchByBarcodeHandlerTests
    {
        private AppDbContext _context = null!;
        private GetBatchByBarcodeHandler _handler = null!;
        private Guid _hospitalId;
        private Guid _storeId;
        private Guid _inventoryItemId;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetBatchByBarcodeHandler(_context);
            _hospitalId = Guid.NewGuid();
            _storeId = Guid.NewGuid();
            _inventoryItemId = Guid.NewGuid();

            _context.InventoryItem.Add(new InventoryItem
            {
                InventoryItemId = _inventoryItemId,
                HospitalId = _hospitalId,
                ItemCode = "PARA-500",
                ItemName = "Paracetamol 500mg",
                Category = "DRUG",
                Unit = "TAB",
                CurrentStock = 100,
                MinStockLevel = 0,
                ReorderQty = 0,
                IsTaxable = true,
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

        private void SeedBatch(string barcodeValue, string status = "ACTIVE", DateTime? expiryDate = null)
        {
            _context.Batch.Add(new Batch
            {
                BatchId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                InventoryItemId = _inventoryItemId,
                StoreId = _storeId,
                BatchNumber = "B-001",
                BarcodeValue = barcodeValue,
                Mrp = 4.5m,
                ExpiryDate = expiryDate ?? DateTime.UtcNow.AddMonths(6),
                ReceivedQty = 50,
                RemainingQty = 50,
                Status = status,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
        }

        [Test]
        public async Task Handle_KnownBarcode_ReturnsMatchingBatchAndItem()
        {
            SeedBatch("8901234567890");
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetBatchByBarcodeRequestModel
            {
                HospitalId = _hospitalId,
                BarcodeValue = "8901234567890",
            }, CancellationToken.None);

            Assert.That(response.Found, Is.True);
            Assert.That(response.InventoryItemId, Is.EqualTo(_inventoryItemId));
            Assert.That(response.ItemName, Is.EqualTo("Paracetamol 500mg"));
            Assert.That(response.Batch, Is.Not.Null);
            Assert.That(response.Batch!.Mrp, Is.EqualTo(4.5m));
        }

        [Test]
        public async Task Handle_UnknownBarcode_ReturnsNotFound()
        {
            SeedBatch("8901234567890");
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetBatchByBarcodeRequestModel
            {
                HospitalId = _hospitalId,
                BarcodeValue = "0000000000000",
            }, CancellationToken.None);

            Assert.That(response.Found, Is.False);
            Assert.That(response.Batch, Is.Null);
        }

        [Test]
        public async Task Handle_ExhaustedBatch_IsNotReturned()
        {
            SeedBatch("8901234567890", status: "EXHAUSTED");
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetBatchByBarcodeRequestModel
            {
                HospitalId = _hospitalId,
                BarcodeValue = "8901234567890",
            }, CancellationToken.None);

            Assert.That(response.Found, Is.False);
        }

        [Test]
        public async Task Handle_BlankBarcode_ReturnsNotFound()
        {
            var response = await _handler.Handle(new GetBatchByBarcodeRequestModel
            {
                HospitalId = _hospitalId,
                BarcodeValue = "   ",
            }, CancellationToken.None);

            Assert.That(response.Found, Is.False);
        }
    }
}
