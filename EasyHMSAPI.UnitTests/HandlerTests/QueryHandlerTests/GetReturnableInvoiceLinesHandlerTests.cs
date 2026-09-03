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
    public class GetReturnableInvoiceLinesHandlerTests
    {
        private AppDbContext _context = null!;
        private GetReturnableInvoiceLinesHandler _handler = null!;
        private Guid _hospitalId;
        private Guid _encounterId;
        private Guid _itemId;
        private Guid _batchId;
        private Guid _chargeId;
        private Guid _chargeEventId;
        private BillingInvoice _invoice = null!;

        [SetUp]
        public async Task SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetReturnableInvoiceLinesHandler(_context);

            _hospitalId = Guid.NewGuid();
            _encounterId = Guid.NewGuid();
            _itemId = Guid.NewGuid();
            _batchId = Guid.NewGuid();
            _chargeId = Guid.NewGuid();
            _chargeEventId = Guid.NewGuid();

            _invoice = new BillingInvoice
            {
                InvoiceId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                EncounterId = _encounterId,
                PatientId = "P1",
                InvoiceNo = "INV-0001",
                InvoiceDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            _context.BillingInvoice.Add(_invoice);

            _context.InventoryItem.Add(new InventoryItem
            {
                InventoryItemId = _itemId,
                HospitalId = _hospitalId,
                ItemCode = "PARA",
                ItemName = "Paracetamol",
                Category = "DRUG",
                Unit = "TAB",
                ChargeId = _chargeId,
                CurrentStock = 0,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });

            _context.Batch.Add(new Batch
            {
                BatchId = _batchId,
                HospitalId = _hospitalId,
                InventoryItemId = _itemId,
                StoreId = Guid.NewGuid(),
                BatchNumber = "B1",
                ExpiryDate = DateTime.UtcNow.AddYears(1),
                RemainingQty = 5,
                ReceivedQty = 10,
                Status = "ACTIVE",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });

            _context.InventoryMovement.Add(new InventoryMovement
            {
                InventoryMovementId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                InventoryItemId = _itemId,
                BatchId = _batchId,
                EncounterId = _encounterId,
                MovementType = "ISSUE",
                Qty = 10,
                MovedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
            });

            _context.BillingChargeEvent.Add(new BillingChargeEvent
            {
                ChargeEventId = _chargeEventId,
                HospitalId = _hospitalId,
                EncounterId = _encounterId,
                ChargeId = _chargeId,
                DisplayName = "Paracetamol",
                Qty = 10,
                UnitPrice = 12,
                NetAmount = 120,
                StatusCode = "POSTED",
                ServiceDate = DateTime.UtcNow,
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
        public async Task Handle_InvoiceNotFound_ReturnsNotFound()
        {
            var response = await _handler.Handle(
                new GetReturnableInvoiceLinesRequestModel { HospitalId = _hospitalId, InvoiceNo = "NOPE" }, CancellationToken.None);

            Assert.That(response.Found, Is.False);
            Assert.That(response.Message, Does.Contain("not found"));
        }

        [Test]
        public async Task Handle_DispensedLineWithNoPriorReturn_ReturnsFullyReturnable()
        {
            var response = await _handler.Handle(
                new GetReturnableInvoiceLinesRequestModel { HospitalId = _hospitalId, InvoiceNo = "INV-0001" }, CancellationToken.None);

            Assert.That(response.Found, Is.True);
            Assert.That(response.Lines, Has.Count.EqualTo(1));
            var line = response.Lines.Single();
            Assert.That(line.ChargeEventId, Is.EqualTo(_chargeEventId));
            Assert.That(line.DispensedQty, Is.EqualTo(10));
            Assert.That(line.AlreadyReturnedQty, Is.EqualTo(0));
            Assert.That(line.ReturnableQty, Is.EqualTo(10));
            Assert.That(line.UnitPrice, Is.EqualTo(12));
            Assert.That(line.IsExpired, Is.False);
        }

        [Test]
        public async Task Handle_PartiallyAlreadyReturned_ReducesReturnableQty()
        {
            _context.PharmacyReturnLine.Add(new PharmacyReturnLine
            {
                ReturnLineId = Guid.NewGuid(),
                ReturnId = Guid.NewGuid(),
                ChargeEventId = _chargeEventId,
                InventoryItemId = _itemId,
                BatchId = _batchId,
                ReturnedQty = 4,
                UnitPrice = 12,
                RefundAmount = 48,
            });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(
                new GetReturnableInvoiceLinesRequestModel { HospitalId = _hospitalId, InvoiceNo = "INV-0001" }, CancellationToken.None);

            var line = response.Lines.Single();
            Assert.That(line.AlreadyReturnedQty, Is.EqualTo(4));
            Assert.That(line.ReturnableQty, Is.EqualTo(6));
        }

        [Test]
        public async Task Handle_FullyReturned_ExcludesLine()
        {
            _context.PharmacyReturnLine.Add(new PharmacyReturnLine
            {
                ReturnLineId = Guid.NewGuid(),
                ReturnId = Guid.NewGuid(),
                ChargeEventId = _chargeEventId,
                InventoryItemId = _itemId,
                BatchId = _batchId,
                ReturnedQty = 10,
                UnitPrice = 12,
                RefundAmount = 120,
            });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(
                new GetReturnableInvoiceLinesRequestModel { HospitalId = _hospitalId, InvoiceNo = "INV-0001" }, CancellationToken.None);

            Assert.That(response.Lines, Is.Empty);
        }
    }
}
