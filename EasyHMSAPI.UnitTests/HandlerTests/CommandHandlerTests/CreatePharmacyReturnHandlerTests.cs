using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Constants;
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
        private Guid _invoiceId;
        private Guid _itemId;
        private Guid _chargeId;
        private Guid _batchId;
        private Guid _chargeEventId;
        private AddPaymentEventRequestModel? _refundPayment;

        [SetUp]
        public async Task SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _mediatorMock = new Mock<IMediator>();
            _handler = new CreatePharmacyReturnHandler(_context, _mediatorMock.Object);

            _hospitalId = Guid.NewGuid();
            _encounterId = Guid.NewGuid();
            _invoiceId = Guid.NewGuid();
            _itemId = Guid.NewGuid();
            _chargeId = Guid.NewGuid();
            _batchId = Guid.NewGuid();
            _chargeEventId = Guid.NewGuid();

            _context.BillingInvoice.Add(new BillingInvoice
            {
                InvoiceId = _invoiceId, HospitalId = _hospitalId, EncounterId = _encounterId, PatientId = "P1", InvoiceNo = "INV-0001",
                InvoiceDate = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            _context.InventoryItem.Add(new InventoryItem
            {
                InventoryItemId = _itemId, HospitalId = _hospitalId, ItemCode = "P", ItemName = "Paracetamol", Category = "DRUG", Unit = "TAB",
                ChargeId = _chargeId, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            _context.Batch.Add(new Batch
            {
                BatchId = _batchId, HospitalId = _hospitalId, InventoryItemId = _itemId, StoreId = Guid.NewGuid(), BatchNumber = "B1",
                ExpiryDate = DateTime.UtcNow.AddYears(1), RemainingQty = 6, ReceivedQty = 10, Status = "ACTIVE",
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            // Sold: 10 units at Rs 12 each, Rs 120 net (12 % GST inside), paid in full.
            _context.BillingChargeEvent.Add(new BillingChargeEvent
            {
                ChargeEventId = _chargeEventId, HospitalId = _hospitalId, EncounterId = _encounterId, PatientId = "P1", ChargeId = _chargeId,
                DisplayName = "Paracetamol", SourceModule = BillingConstants.SourceModule.PharmacyCounter, SourceRefId = _batchId.ToString(),
                Qty = 10, UnitPrice = 12, GrossAmount = 120, DiscountAmount = 0, NetAmount = 120, TaxableAmount = 107.14m, TaxAmount = 12.86m,
                StatusCode = BillingConstants.ChargeEventStatus.Posted, CreatedAt = DateTime.UtcNow,
            });
            _context.BillingInvoiceChargeEvent.Add(new BillingInvoiceChargeEvent { InvoiceId = _invoiceId, ChargeEventId = _chargeEventId });
            _context.BillingPayment.Add(new BillingPayment
            {
                PaymentId = Guid.NewGuid(), HospitalId = _hospitalId, EncounterId = _encounterId, PaymentType = BillingConstants.PaymentType.Payment,
                Amount = 120, PaidAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            _context.InventoryMovement.Add(new InventoryMovement
            {
                InventoryMovementId = Guid.NewGuid(), HospitalId = _hospitalId, InventoryItemId = _itemId, BatchId = _batchId, EncounterId = _encounterId,
                MovementType = "ISSUE", Qty = 10, MovedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();

            _mediatorMock.Setup(m => m.Send(It.IsAny<RecordInventoryMovementRequestModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RecordInventoryMovementResponseModel { Success = true });
            _refundPayment = null;
            _mediatorMock.Setup(m => m.Send(It.IsAny<AddPaymentEventRequestModel>(), It.IsAny<CancellationToken>()))
                .Returns<AddPaymentEventRequestModel, CancellationToken>((req, _) =>
                {
                    _refundPayment = req;
                    // The real handler records the refund as a payment row; mirror that so a second refund sees it.
                    _context.BillingPayment.Add(new BillingPayment
                    {
                        PaymentId = Guid.NewGuid(), HospitalId = req.HospitalId, EncounterId = req.EncounterId, PaymentType = req.Payment!.PaymentType,
                        Amount = req.Payment.Amount, PaidAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
                    });
                    _context.SaveChanges();
                    return Task.FromResult(new AddPaymentEventResponseModel { Success = true });
                });
        }

        [TearDown]
        public void TearDown()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        private CreatePharmacyReturnRequestModel ValidRequest(decimal qty = 4, decimal clientPrice = 12) => new()
        {
            HospitalId = _hospitalId, InvoiceNo = "INV-0001", RefundMode = "CASH", LoggedInUserName = "tester",
            Lines = new List<PharmacyReturnLineInput>
            {
                new() { ChargeEventId = _chargeEventId, InventoryItemId = _itemId, BatchId = _batchId, ReturnedQty = qty, UnitPrice = clientPrice },
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
            var response = await _handler.Handle(ValidRequest(qty: 999), CancellationToken.None);

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
        public async Task Handle_ValidReturn_RestocksReversesTheChargeAndPaysTheCashBack()
        {
            var response = await _handler.Handle(ValidRequest(), CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            Assert.That(response.TotalRefundAmount, Is.EqualTo(48m));
            Assert.That(response.CashRefundAmount, Is.EqualTo(48m));
            Assert.That(response.ReturnNo, Is.Not.Null.And.Not.Empty);

            _mediatorMock.Verify(m => m.Send(
                It.Is<RecordInventoryMovementRequestModel>(r => r.MovementType == "RETURN" && r.Qty == 4 && r.BatchId == _batchId),
                It.IsAny<CancellationToken>()), Times.Once);

            var savedLine = _context.PharmacyReturnLine.Single();
            Assert.That(savedLine.ReturnedQty, Is.EqualTo(4));
            Assert.That(savedLine.UnitPrice, Is.EqualTo(12m));

            // Reversing charge event: negative, linked to the original, so sales/GST/analytics are net of the return.
            var reversal = _context.BillingChargeEvent.Single(c => c.SourceRefId == _chargeEventId.ToString());
            Assert.That(reversal.Qty, Is.EqualTo(-4m));
            Assert.That(reversal.NetAmount, Is.EqualTo(-48m));
            Assert.That(reversal.DiscountAmount, Is.EqualTo(0m), "CK_BCE_Discount forbids a negative discount");
            Assert.That(reversal.TaxAmount, Is.EqualTo(-5.14m));
            Assert.That(reversal.SourceModule, Is.EqualTo(BillingConstants.SourceModule.PharmacyCounter));

            Assert.That(_refundPayment, Is.Not.Null);
            Assert.That(_refundPayment!.Payment!.PaymentType, Is.EqualTo(BillingConstants.PaymentType.Refund));
            Assert.That(_refundPayment.Payment.Amount, Is.EqualTo(48m));
        }

        [Test]
        public async Task Handle_ClientUnitPriceIsIgnored_RefundComesFromTheOriginalCharge()
        {
            var response = await _handler.Handle(ValidRequest(clientPrice: 9999), CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            Assert.That(response.TotalRefundAmount, Is.EqualTo(48m));
            Assert.That(_context.PharmacyReturnLine.Single().RefundAmount, Is.EqualTo(48m));
        }

        [Test]
        public async Task Handle_DiscountedSale_RefundsWhatWasActuallyCharged()
        {
            var ev = await _context.BillingChargeEvent.FindAsync(_chargeEventId);
            ev!.DiscountAmount = 12; ev.NetAmount = 108; // 10 % off: Rs 10.80 per unit
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(ValidRequest(qty: 5), CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            Assert.That(response.TotalRefundAmount, Is.EqualTo(54m));
        }

        [Test]
        public async Task Handle_ChargeEventOfAnotherInvoice_IsRejected()
        {
            var foreignEvent = Guid.NewGuid();
            _context.BillingChargeEvent.Add(new BillingChargeEvent
            {
                ChargeEventId = foreignEvent, HospitalId = _hospitalId, EncounterId = Guid.NewGuid(), ChargeId = _chargeId, DisplayName = "x",
                Qty = 100, UnitPrice = 500, NetAmount = 50000, StatusCode = BillingConstants.ChargeEventStatus.Posted, CreatedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();
            var request = ValidRequest();
            request.Lines[0].ChargeEventId = foreignEvent;

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("do not belong to this invoice"));
            _mediatorMock.Verify(m => m.Send(It.IsAny<RecordInventoryMovementRequestModel>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task Handle_ItemOrBatchNotMatchingTheSoldLine_IsRejected()
        {
            var otherItem = Guid.NewGuid();
            _context.InventoryItem.Add(new InventoryItem { InventoryItemId = otherItem, HospitalId = _hospitalId, ItemCode = "O", ItemName = "Other", Category = "DRUG", Unit = "TAB", ChargeId = Guid.NewGuid(), IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            var otherBatch = Guid.NewGuid();
            _context.Batch.Add(new Batch { BatchId = otherBatch, HospitalId = _hospitalId, InventoryItemId = _itemId, StoreId = Guid.NewGuid(), BatchNumber = "B2", ExpiryDate = DateTime.UtcNow.AddYears(1), RemainingQty = 5, ReceivedQty = 5, Status = "ACTIVE", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            await _context.SaveChangesAsync();

            var wrongItem = ValidRequest();
            wrongItem.Lines[0].InventoryItemId = otherItem;
            var wrongBatch = ValidRequest();
            wrongBatch.Lines[0].BatchId = otherBatch;

            Assert.That((await _handler.Handle(wrongItem, CancellationToken.None)).Message, Does.Contain("does not match"));
            Assert.That((await _handler.Handle(wrongBatch, CancellationToken.None)).Message, Does.Contain("batch does not match"));
        }

        [Test]
        public async Task Handle_TwoReturnsAgainstTheSameLine_CannotExceedWhatWasSold()
        {
            var first = await _handler.Handle(ValidRequest(qty: 4), CancellationToken.None);
            var second = await _handler.Handle(ValidRequest(qty: 7), CancellationToken.None);
            var third = await _handler.Handle(ValidRequest(qty: 6), CancellationToken.None);

            Assert.That(first.Success, Is.True, first.Message);
            Assert.That(second.Success, Is.False);
            Assert.That(second.Message, Does.Contain("only 6 remains returnable"));
            Assert.That(third.Success, Is.True, third.Message);
        }

        [Test]
        public async Task Handle_CreditSaleNotPaid_ReducesTheDueButPaysNothingOut()
        {
            _context.BillingPayment.RemoveRange(_context.BillingPayment.ToList());
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(ValidRequest(), CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            Assert.That(response.TotalRefundAmount, Is.EqualTo(48m));
            Assert.That(response.CashRefundAmount, Is.EqualTo(0m));
            Assert.That(_refundPayment, Is.Null);
        }

        [Test]
        public async Task Handle_PartPaidSale_RefundsOnlyTheExcessOverTheReducedBill()
        {
            // Customer paid Rs 100 of a Rs 120 bill; returning Rs 48 leaves a Rs 72 bill, so Rs 28 is owed back.
            var payment = _context.BillingPayment.Single();
            payment.Amount = 100;
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(ValidRequest(), CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            Assert.That(response.CashRefundAmount, Is.EqualTo(28m));
            Assert.That(_refundPayment!.Payment!.Amount, Is.EqualTo(28m));
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

        [Test]
        public async Task Handle_RefundPaymentFails_FailsTheReturn()
        {
            _mediatorMock.Setup(m => m.Send(It.IsAny<AddPaymentEventRequestModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AddPaymentEventResponseModel { Success = false, Message = "no credit" });

            var response = await _handler.Handle(ValidRequest(), CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("Refund payment failed"));
        }
    }
}
