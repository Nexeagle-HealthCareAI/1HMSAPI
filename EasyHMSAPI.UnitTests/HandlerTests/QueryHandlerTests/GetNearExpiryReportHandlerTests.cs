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
    public class GetNearExpiryReportHandlerTests
    {
        private AppDbContext _context = null!;
        private GetNearExpiryReportHandler _handler = null!;
        private Guid _hospitalId;
        private Guid _storeId;
        private Guid _itemId;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetNearExpiryReportHandler(_context);
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
                CurrentStock = 100,
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

        private void SeedBatch(int daysToExpiry, decimal remainingQty = 10, string status = "ACTIVE")
        {
            _context.Batch.Add(new Batch
            {
                BatchId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                InventoryItemId = _itemId,
                StoreId = _storeId,
                BatchNumber = $"B-{Guid.NewGuid().ToString().Substring(0, 5)}",
                ExpiryDate = DateTime.UtcNow.Date.AddDays(daysToExpiry),
                ReceivedQty = remainingQty,
                RemainingQty = remainingQty,
                Status = status,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
        }

        [Test]
        public async Task Handle_BucketsBatchesCorrectly()
        {
            SeedBatch(daysToExpiry: 10);   // RED
            SeedBatch(daysToExpiry: 60);   // ORANGE
            SeedBatch(daysToExpiry: 150);  // YELLOW
            SeedBatch(daysToExpiry: 300);  // outside 180-day window -> excluded entirely
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetNearExpiryReportRequestModel { HospitalId = _hospitalId }, CancellationToken.None);

            Assert.That(response.Batches, Has.Count.EqualTo(3));
            Assert.That(response.Batches.Select(b => b.Bucket), Is.EquivalentTo(new[] { "RED", "ORANGE", "YELLOW" }));
            Assert.That(response.Batches, Is.Ordered.By(nameof(EasyHMSAPI.Application.ResponseModels.QueryResponseModels.NearExpiryBatchDataModel.DaysToExpiry)));
        }

        [Test]
        public async Task Handle_ExhaustedOrExpiredStatus_IsExcluded()
        {
            SeedBatch(daysToExpiry: 10, status: "EXHAUSTED");
            SeedBatch(daysToExpiry: 5, remainingQty: 0);
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetNearExpiryReportRequestModel { HospitalId = _hospitalId }, CancellationToken.None);

            Assert.That(response.Batches, Is.Empty);
        }

        [Test]
        public async Task Handle_BucketFilter_ReturnsOnlyMatchingBucket()
        {
            SeedBatch(daysToExpiry: 10);   // RED
            SeedBatch(daysToExpiry: 60);   // ORANGE
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetNearExpiryReportRequestModel { HospitalId = _hospitalId, Bucket = "RED" }, CancellationToken.None);

            Assert.That(response.Batches, Has.Count.EqualTo(1));
            Assert.That(response.Batches[0].Bucket, Is.EqualTo("RED"));
        }

        [Test]
        public async Task Handle_StoreFilter_ScopesToThatStoreOnly()
        {
            var otherStoreId = Guid.NewGuid();
            SeedBatch(daysToExpiry: 10);
            _context.Batch.Add(new Batch
            {
                BatchId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                InventoryItemId = _itemId,
                StoreId = otherStoreId,
                BatchNumber = "OTHER-STORE",
                ExpiryDate = DateTime.UtcNow.Date.AddDays(10),
                ReceivedQty = 5,
                RemainingQty = 5,
                Status = "ACTIVE",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetNearExpiryReportRequestModel { HospitalId = _hospitalId, StoreId = _storeId }, CancellationToken.None);

            Assert.That(response.Batches, Has.Count.EqualTo(1));
            Assert.That(response.Batches[0].StoreId, Is.EqualTo(_storeId));
        }
    }
}
