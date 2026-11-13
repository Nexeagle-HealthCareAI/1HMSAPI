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
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    /// <summary>
    /// The pharmacy counter used to post whatever rate / discount the browser sent, dispense unbilled items for
    /// free in a mixed cart, and sell twice on a retry. Pricing, the discount cap, unbilled-item rejection and
    /// idempotency are now enforced on the server.
    /// </summary>
    [TestFixture]
    public class PharmacyCheckoutPricingTests
    {
        private AppDbContext _context = null!;
        private Mock<IMediator> _mediator = null!;
        private PharmacyRetailCheckoutCommandHandler _handler = null!;
        private readonly Guid _hospitalId = Guid.NewGuid();
        private readonly Guid _storeId = Guid.NewGuid();
        private AddChargeEventRequestModel? _postedCharges;
        private AddPaymentEventRequestModel? _payment;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _mediator = new Mock<IMediator>();
            _handler = new PharmacyRetailCheckoutCommandHandler(_context, _mediator.Object, NullLogger<PharmacyRetailCheckoutCommandHandler>.Instance, UsageLimitTestHelper.AlwaysAllow());
            _postedCharges = null;
            _payment = null;

            _mediator.Setup(m => m.Send(It.IsAny<AddChargeEventRequestModel>(), It.IsAny<CancellationToken>()))
                .Returns<AddChargeEventRequestModel, CancellationToken>((req, _) =>
                {
                    _postedCharges = req;
                    var details = new List<ChargeEventDetail>();
                    foreach (var c in req.Charges!)
                    {
                        var id = Guid.NewGuid();
                        var net = c.Qty * c.Rate * (1 - c.DiscountPercent / 100m);
                        _context.BillingChargeEvent.Add(new BillingChargeEvent
                        {
                            ChargeEventId = id, HospitalId = req.HospitalId, EncounterId = req.EncounterId, PatientId = req.PatientId,
                            ChargeId = c.ChargeId, DisplayName = c.DisplayName ?? "x", Qty = c.Qty, UnitPrice = c.Rate,
                            GrossAmount = c.Qty * c.Rate, DiscountAmount = 0, NetAmount = net, TaxAmount = 0,
                            SourceRefId = c.SourceRefId, IdempotencyKey = req.IdempotencyKey, CreatedAt = DateTime.UtcNow,
                        });
                        details.Add(new ChargeEventDetail { ChargeEventId = id });
                    }
                    _context.SaveChanges();
                    return Task.FromResult(new AddChargeEventResponseModel { Success = true, Data = new AddChargesData { ChargeEvents = details } });
                });
            _mediator.Setup(m => m.Send(It.IsAny<AddPaymentEventRequestModel>(), It.IsAny<CancellationToken>()))
                .Returns<AddPaymentEventRequestModel, CancellationToken>((req, _) => { _payment = req; return Task.FromResult(new AddPaymentEventResponseModel { Success = true }); });
        }

        [TearDown]
        public void TearDown()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        private (InventoryItem item, ChargeMaster master) Seed(string name, decimal defaultRate = 2m, decimal? maxDiscount = null, bool billable = true)
        {
            var chargeId = Guid.NewGuid();
            var master = new ChargeMaster { ChargeId = chargeId, HospitalId = _hospitalId, DisplayName = name, DefaultRate = defaultRate, MaxDiscountPercent = maxDiscount, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            var item = new InventoryItem
            {
                InventoryItemId = Guid.NewGuid(), HospitalId = _hospitalId, ItemCode = name.ToUpperInvariant(), ItemName = name, Category = "DRUG", Unit = "TAB",
                CurrentStock = 100, ChargeId = billable ? chargeId : null, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            if (billable) _context.ChargeMaster.Add(master);
            _context.InventoryItem.Add(item);
            _context.SaveChanges();
            return (item, master);
        }

        private void MovementReturns(params AllocatedBatchDetail[] allocations) =>
            _mediator.Setup(m => m.Send(It.IsAny<RecordInventoryMovementRequestModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RecordInventoryMovementResponseModel { Success = true, InventoryMovementId = Guid.NewGuid(), AllocatedBatchDetails = allocations.ToList() });

        private PharmacyRetailCheckoutCommand Request(Guid itemId, decimal qty = 10, decimal clientRate = 2, decimal discount = 0) => new()
        {
            HospitalId = _hospitalId, StoreId = _storeId, PatientId = "PT-1", PayInFull = true, PaymentMode = "CASH",
            Items = new List<PharmacyCartItem> { new() { InventoryItemId = itemId, Qty = qty, Rate = clientRate, DiscountPercent = discount } },
        };

        [Test]
        public async Task TamperedClientRate_IsIgnored_PriceComesFromBatchMrp()
        {
            var (item, _) = Seed("Amoxicillin");
            MovementReturns(new AllocatedBatchDetail { BatchId = Guid.NewGuid(), BatchNumber = "B1", Mrp = 8.5m, AllocatedQty = 10 });

            var response = await _handler.Handle(Request(item.InventoryItemId, clientRate: 0.01m), CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            Assert.That(_postedCharges!.Charges!.Single().Rate, Is.EqualTo(8.5m));
        }

        [Test]
        public async Task NoMrp_FallsBackToChargeMasterDefaultRate()
        {
            var (item, _) = Seed("Cetirizine", defaultRate: 3m);
            MovementReturns(new AllocatedBatchDetail { BatchId = Guid.NewGuid(), BatchNumber = "B1", Mrp = null, AllocatedQty = 10 });

            var response = await _handler.Handle(Request(item.InventoryItemId, clientRate: 99), CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            Assert.That(_postedCharges!.Charges!.Single().Rate, Is.EqualTo(3m));
        }

        [Test]
        public async Task NoPriceAnywhere_IsRejected()
        {
            var (item, _) = Seed("Mystery", defaultRate: 0m);
            MovementReturns(new AllocatedBatchDetail { BatchId = Guid.NewGuid(), BatchNumber = "B9", Mrp = null, AllocatedQty = 10 });

            var response = await _handler.Handle(Request(item.InventoryItemId), CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("No price is configured"));
            Assert.That(_postedCharges, Is.Null);
        }

        [Test]
        public async Task ItemSplitAcrossBatches_IsChargedPerBatchAtEachBatchMrp()
        {
            var (item, _) = Seed("Metformin");
            var b1 = Guid.NewGuid();
            var b2 = Guid.NewGuid();
            MovementReturns(
                new AllocatedBatchDetail { BatchId = b1, BatchNumber = "OLD", Mrp = 4m, AllocatedQty = 6 },
                new AllocatedBatchDetail { BatchId = b2, BatchNumber = "NEW", Mrp = 5m, AllocatedQty = 4 });

            var response = await _handler.Handle(Request(item.InventoryItemId), CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            var lines = _postedCharges!.Charges!;
            Assert.That(lines, Has.Count.EqualTo(2));
            Assert.That(lines.Select(l => (l.Qty, l.Rate)), Is.EquivalentTo(new[] { (6m, 4m), (4m, 5m) }));
            Assert.That(lines.Select(l => l.SourceRefId), Is.EquivalentTo(new[] { b1.ToString(), b2.ToString() }), "each line remembers its batch for returns");
        }

        [Test]
        public async Task Discount_AboveHospitalDefault20Percent_IsRejected_BeforeAnyStockMoves()
        {
            var (item, _) = Seed("Ibuprofen");

            var response = await _handler.Handle(Request(item.InventoryItemId, discount: 25), CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("cannot exceed 20"));
            _mediator.Verify(m => m.Send(It.IsAny<RecordInventoryMovementRequestModel>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task Discount_UsesChargeMasterCap_ThenHospitalPolicyCap()
        {
            var (capped, _) = Seed("Capped", maxDiscount: 5m);
            var (open, _) = Seed("Open");
            _context.BillingPolicy.Add(new BillingPolicy { BillingPolicyId = Guid.NewGuid(), HospitalId = _hospitalId, PharmacyMaxDiscountPercent = 40m, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            _context.SaveChanges();
            MovementReturns(new AllocatedBatchDetail { BatchId = Guid.NewGuid(), BatchNumber = "B", Mrp = 10m, AllocatedQty = 10 });

            var overItemCap = await _handler.Handle(Request(capped.InventoryItemId, discount: 10), CancellationToken.None);
            var withinPolicy = await _handler.Handle(Request(open.InventoryItemId, discount: 35), CancellationToken.None);
            var overPolicy = await _handler.Handle(Request(open.InventoryItemId, discount: 45), CancellationToken.None);

            Assert.That(overItemCap.Success, Is.False);
            Assert.That(overItemCap.Message, Does.Contain("cannot exceed 5"));
            Assert.That(withinPolicy.Success, Is.True, withinPolicy.Message);
            Assert.That(overPolicy.Success, Is.False);
            Assert.That(overPolicy.Message, Does.Contain("cannot exceed 40"));
        }

        [Test]
        public async Task InvalidDiscountOrQuantity_IsRejected()
        {
            var (item, _) = Seed("Aspirin");

            Assert.That((await _handler.Handle(Request(item.InventoryItemId, discount: -1), CancellationToken.None)).Success, Is.False);
            Assert.That((await _handler.Handle(Request(item.InventoryItemId, discount: 101), CancellationToken.None)).Success, Is.False);
            Assert.That((await _handler.Handle(Request(item.InventoryItemId, qty: 0), CancellationToken.None)).Success, Is.False);
        }

        [Test]
        public async Task MixedCart_WithAnUnbilledItem_IsRejectedWholesale_NothingDispensed()
        {
            var (billable, _) = Seed("Billable");
            var (free, _) = Seed("Unlinked", billable: false);
            var request = Request(billable.InventoryItemId);
            request.Items.Add(new PharmacyCartItem { InventoryItemId = free.InventoryItemId, Qty = 1 });

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("Unlinked"));
            _mediator.Verify(m => m.Send(It.IsAny<RecordInventoryMovementRequestModel>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task ItemOfAnotherHospital_IsRejected()
        {
            var (item, _) = Seed("Mine");
            var request = Request(item.InventoryItemId);
            request.HospitalId = Guid.NewGuid();

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("not found"));
        }

        [Test]
        public async Task RepeatedIdempotencyKey_ReturnsTheFirstSale_WithoutSecondStockIssueOrPayment()
        {
            var (item, _) = Seed("Dolo");
            MovementReturns(new AllocatedBatchDetail { BatchId = Guid.NewGuid(), BatchNumber = "B1", Mrp = 2m, AllocatedQty = 10 });
            var request = Request(item.InventoryItemId);
            request.IdempotencyKey = "attempt-1";

            var first = await _handler.Handle(request, CancellationToken.None);
            var second = await _handler.Handle(Request(item.InventoryItemId), CancellationToken.None); // new key-less request: a different sale
            var replay = await _handler.Handle(new PharmacyRetailCheckoutCommand
            {
                HospitalId = _hospitalId, StoreId = _storeId, PatientId = "PT-1", IdempotencyKey = "attempt-1",
                Items = request.Items,
            }, CancellationToken.None);

            Assert.That(first.Success, Is.True, first.Message);
            Assert.That(second.Success, Is.True);
            Assert.That(replay.Success, Is.True);
            Assert.That(replay.IsReplay, Is.True);
            Assert.That(replay.InvoiceNo, Is.EqualTo(first.InvoiceNo));
            Assert.That(replay.InvoiceId, Is.EqualTo(first.InvoiceId));
            _mediator.Verify(m => m.Send(It.IsAny<RecordInventoryMovementRequestModel>(), It.IsAny<CancellationToken>()), Times.Exactly(2), "first + the unrelated second sale; the replay issues nothing");
        }

        [Test]
        public async Task PayInFull_CollectsTheServerInvoiceTotal_NotTheClientEstimate()
        {
            var (item, _) = Seed("Azithro");
            MovementReturns(new AllocatedBatchDetail { BatchId = Guid.NewGuid(), BatchNumber = "B1", Mrp = 10m, AllocatedQty = 10 });
            var request = Request(item.InventoryItemId, clientRate: 1);
            request.PaidAmount = 10; // browser thought the bill was Rs 10; the server prices it at Rs 100

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            Assert.That(_payment!.Payment!.Amount, Is.EqualTo(100m));
        }

        [Test]
        public async Task ExplicitPaidAmount_CannotExceedTheInvoiceTotal()
        {
            var (item, _) = Seed("Azithro");
            MovementReturns(new AllocatedBatchDetail { BatchId = Guid.NewGuid(), BatchNumber = "B1", Mrp = 10m, AllocatedQty = 10 });
            var request = Request(item.InventoryItemId);
            request.PayInFull = false;
            request.PaidAmount = 500;

            await _handler.Handle(request, CancellationToken.None);
            Assert.That(_payment!.Payment!.Amount, Is.EqualTo(100m));

            _payment = null;
            request.IdempotencyKey = null;
            request.PaidAmount = 40; // credit sale: part payment is allowed
            await _handler.Handle(request, CancellationToken.None);
            Assert.That(_payment!.Payment!.Amount, Is.EqualTo(40m));
        }
    }
}
