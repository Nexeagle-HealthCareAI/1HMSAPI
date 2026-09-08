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
    public class VendorReturnQueryHandlersTests
    {
        private AppDbContext _context = null!;
        private VendorReturnQueryHandlers _handler = null!;
        private Guid _hospitalId;
        private Guid _vendorId;
        private Guid _itemId;

        [SetUp]
        public async Task SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new VendorReturnQueryHandlers(_context);

            _hospitalId = Guid.NewGuid();
            _vendorId = Guid.NewGuid();
            _itemId = Guid.NewGuid();

            _context.Vendor.Add(new Vendor
            {
                VendorId = _vendorId,
                HospitalId = _hospitalId,
                VendorCode = "V1",
                VendorName = "Acme Pharma",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });

            _context.InventoryItem.Add(new InventoryItem
            {
                InventoryItemId = _itemId,
                HospitalId = _hospitalId,
                ItemCode = "PARA",
                ItemName = "Paracetamol",
                Category = "DRUG",
                Unit = "TAB",
                CurrentStock = 0,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });

            // Near-expiry, eligible batch.
            _context.Batch.Add(new Batch
            {
                BatchId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                InventoryItemId = _itemId,
                StoreId = Guid.NewGuid(),
                VendorId = _vendorId,
                BatchNumber = "NEAR-EXP",
                ExpiryDate = DateTime.UtcNow.AddDays(20),
                RemainingQty = 5,
                UnitCost = 10,
                Status = "ACTIVE",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });

            // Far-out batch, not yet eligible.
            _context.Batch.Add(new Batch
            {
                BatchId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                InventoryItemId = _itemId,
                StoreId = Guid.NewGuid(),
                VendorId = _vendorId,
                BatchNumber = "FAR-EXP",
                ExpiryDate = DateTime.UtcNow.AddDays(400),
                RemainingQty = 5,
                UnitCost = 10,
                Status = "ACTIVE",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });

            // Exhausted batch — should never show up regardless of expiry.
            _context.Batch.Add(new Batch
            {
                BatchId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                InventoryItemId = _itemId,
                StoreId = Guid.NewGuid(),
                VendorId = _vendorId,
                BatchNumber = "EXHAUSTED",
                ExpiryDate = DateTime.UtcNow.AddDays(10),
                RemainingQty = 0,
                UnitCost = 10,
                Status = "EXHAUSTED",
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

        [Test]
        public async Task Handle_EligibleBatches_ReturnsOnlyActiveWithinWindow()
        {
            var response = await _handler.Handle(
                new GetRtvEligibleBatchesRequestModel { HospitalId = _hospitalId, VendorId = _vendorId, DaysWindow = 60 }, CancellationToken.None);

            Assert.That(response.Batches, Has.Count.EqualTo(1));
            var row = response.Batches.Single();
            Assert.That(row.BatchNumber, Is.EqualTo("NEAR-EXP"));
            Assert.That(row.ItemName, Is.EqualTo("Paracetamol"));
            Assert.That(row.EstimatedValue, Is.EqualTo(50));
        }

        [Test]
        public async Task Handle_VendorReturns_EmptyWhenNoneGenerated()
        {
            var response = await _handler.Handle(
                new GetVendorReturnsRequestModel { HospitalId = _hospitalId, VendorId = _vendorId }, CancellationToken.None);

            Assert.That(response.Returns, Is.Empty);
        }

        [Test]
        public async Task Handle_VendorReturns_ReturnsNoteWithItemNamesResolved()
        {
            var noteId = Guid.NewGuid();
            _context.VendorReturnNote.Add(new VendorReturnNote
            {
                VendorReturnId = noteId,
                HospitalId = _hospitalId,
                VendorId = _vendorId,
                ReturnNoteNo = "RTV-2026-000001",
                TotalQty = 5,
                TotalValue = 50,
                GeneratedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
            });
            _context.VendorReturnLine.Add(new VendorReturnLine
            {
                VendorReturnLineId = Guid.NewGuid(),
                VendorReturnId = noteId,
                InventoryItemId = _itemId,
                BatchId = Guid.NewGuid(),
                BatchNumber = "NEAR-EXP",
                Qty = 5,
                UnitCost = 10,
                LineValue = 50,
            });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(
                new GetVendorReturnsRequestModel { HospitalId = _hospitalId }, CancellationToken.None);

            Assert.That(response.Returns, Has.Count.EqualTo(1));
            var note = response.Returns.Single();
            Assert.That(note.VendorName, Is.EqualTo("Acme Pharma"));
            Assert.That(note.Lines, Has.Count.EqualTo(1));
            Assert.That(note.Lines[0].ItemName, Is.EqualTo("Paracetamol"));
        }
    }
}
