using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using MediatR;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class CreatePharmacyReturnHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<IMediator> _mediatorMock = null!;
        private CreatePharmacyReturnHandler _handler = null!;
        private Guid _hospitalId;
        private Guid _encounterId;
        private Guid _itemId;
        private Guid _batchId;
        private Guid _chargeEventId;

        [SetUp]
        public async Task SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _mediatorMock = new Mock<IMediator>();
            _handler = new CreatePharmacyReturnHandler(_context, _mediatorMock.Object);

            _hospitalId = Guid.NewGuid();
            _encounterId = Guid.NewGuid();
            _itemId = Guid.NewGuid();
            _batchId = Guid.NewGuid();
            _chargeEventId = Guid.NewGuid();

            _context.BillingInvoice.Add(new BillingInvoice
            {
                InvoiceId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                EncounterId = _encounterId,
                PatientId = "P1",
                InvoiceNo = "INV-0001",
                InvoiceDate = DateTime.UtcNow,
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
                RemainingQty = 6,
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

            await _context.SaveChangesAsync();

            _mediatorMock
                .Setup(m => m.Send(It.IsAny<RecordInventoryMovementRequestModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RecordInventoryMovementResponseModel { Success = true });
        }

        [TearDown]
        public void TearDown()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        private CreatePharmacyReturnRequestModel ValidRequest() => new()
        {
            HospitalId = _hospitalId,
            InvoiceNo = "INV-0001",
            RefundMode = "CASH",
            LoggedInUserName = "tester",
            Lines = new List<PharmacyReturnLineInput>
            {
                new() { ChargeEventId = _chargeEventId, InventoryItemId = _itemId, BatchId = _batchId, ReturnedQty = 4, UnitPrice = 12 },
            },
        };

        [Test]
        public async Task Handle_MissingInvoiceNo_ReturnsError()
        {
            var response = await _handler.Handle(new CreatePharmacyReturnRequestModel { HospitalId = _hospitalId }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("required"));
        }

        [Test]
        public async Task Handle_NoLines_ReturnsError()
        {
            var request = ValidRequest();
            request.Lines = new List<PharmacyReturnLineInput>();

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("At least one"));
        }

        [Test]
        public async Task Handle_InvoiceNotFound_ReturnsError()
        {
            var request = ValidRequest();
            request.InvoiceNo = "NOPE";

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("Invoice not found"));
        }

        [Test]
        public async Task Handle_ExceedsReturnableQty_ReturnsError()
        {
            var request = ValidRequest();
            request.Lines[0].ReturnedQty = 999;

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("returnable"));
            _mediatorMock.Verify(m => m.Send(It.IsAny<RecordInventoryMovementRequestModel>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task Handle_ExpiredBatch_ReturnsError()
        {
            var batch = await _context.Batch.FindAsync(_batchId);
            batch!.ExpiryDate = DateTime.UtcNow.AddDays(-5);
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(ValidRequest(), CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("expired"));
        }

        [Test]
        public async Task Handle_ValidReturn_RestocksAndRecordsLedger()
        {
            var response = await _handler.Handle(ValidRequest(), CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.TotalRefundAmount, Is.EqualTo(48));
            Assert.That(response.ReturnNo, Is.Not.Null.And.Not.Empty);

            _mediatorMock.Verify(m => m.Send(
                It.Is<RecordInventoryMovementRequestModel>(r => r.MovementType == "RETURN" && r.Qty == 4 && r.BatchId == _batchId),
                It.IsAny<CancellationToken>()), Times.Once);

            var savedReturn = _context.PharmacyReturn.Single();
            Assert.That(savedReturn.TotalRefundAmount, Is.EqualTo(48));
            var savedLine = _context.PharmacyReturnLine.Single();
            Assert.That(savedLine.ReturnedQty, Is.EqualTo(4));
        }

        [Test]
        public async Task Handle_StockReversalFails_RollsBackAndReturnsError()
        {
            _mediatorMock
                .Setup(m => m.Send(It.IsAny<RecordInventoryMovementRequestModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RecordInventoryMovementResponseModel { Success = false, Message = "boom" });

            var response = await _handler.Handle(ValidRequest(), CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("boom"));
            Assert.That(_context.PharmacyReturn.Any(), Is.False);
        }
    }
}
