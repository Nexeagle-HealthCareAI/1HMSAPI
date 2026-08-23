using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class AddPaymentEventHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<IMediator> _mediatorMock = null!;
        private AddPaymentEventHandler _handler = null!;
        private Guid _hospitalId;
        private Guid _encounterId;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _mediatorMock = new Mock<IMediator>();
            _handler = new AddPaymentEventHandler(_context, _mediatorMock.Object);
            _hospitalId = Guid.NewGuid();
            _encounterId = Guid.NewGuid();
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        // Seeds a charge + a FINALIZED-avoiding DRAFT invoice worth `net`, so payments have
        // something to allocate against without CreateDraftInvoiceHandler needing to run
        // (mediator is mocked, so it wouldn't actually build one). Also seeds the matching
        // Encounter row -- required since AddPaymentEventHandler now validates
        // EncounterId+HospitalId+PatientId against a real Encounter before doing anything else
        // (see Handle_EncounterBelongsToAnotherHospital_ReturnsNotFound_DoesNotPost below for
        // why that check exists).
        private BillingInvoice SeedInvoiceWithCharge(decimal net)
        {
            _context.Encounter.Add(new Encounter
            {
                EncounterId = _encounterId,
                HospitalId = _hospitalId,
                PatientId = "PT001",
                StatusCode = "OPEN",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });

            var charge = new BillingChargeEvent
            {
                ChargeEventId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EncounterId = _encounterId,
                DisplayName = "Consult",
                CategoryCode = "CONSULT",
                Qty = 1,
                UnitPrice = net,
                GrossAmount = net,
                DiscountAmount = 0,
                NetAmount = net,
                StatusCode = "POSTED",
                ServiceDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            _context.BillingChargeEvent.Add(charge);

            var invoice = new BillingInvoice
            {
                InvoiceId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                EncounterId = _encounterId,
                PatientId = "PT001",
                InvoiceNo = "INV-1",
                InvoiceDate = DateTime.UtcNow,
                StatusCode = BillingConstants.InvoiceStatus.Draft,
                GrossAmount = net,
                DiscountAmount = 0,
                NetAmount = net,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            _context.BillingInvoice.Add(invoice);
            _context.BillingInvoiceChargeEvent.Add(new BillingInvoiceChargeEvent { InvoiceId = invoice.InvoiceId, ChargeEventId = charge.ChargeEventId });
            _context.SaveChanges();
            return invoice;
        }

        [Test]
        public async Task Handle_Advance_ExceedingDue_HoldsExcessAsCredit_NoApprovalNeeded()
        {
            SeedInvoiceWithCharge(net: 500);

            var response = await _handler.Handle(new AddPaymentEventRequestModel
            {
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EncounterId = _encounterId,
                Payment = new PaymentDetail { PaymentType = "ADVANCE", PaymentMode = "CASH", Amount = 800 },
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.PendingApproval, Is.Not.True, "Advance-into-credit must apply directly, not require approval.");
            Assert.That(response.Data!.AllocatedAmount, Is.EqualTo(500));
            Assert.That(response.Data.CreditAmount, Is.EqualTo(300));
            Assert.That(_context.CreditApproval.Count(), Is.EqualTo(0));

            var payment = _context.BillingPayment.Single(p => p.EncounterId == _encounterId);
            Assert.That(payment.Amount, Is.EqualTo(800));
        }

        [Test]
        public async Task Handle_Refund_PartialAmount_StillLeavingCredit_AppliesDirectly_NoApprovalNeeded()
        {
            var invoice = SeedInvoiceWithCharge(net: 0); // fully-credit scenario: nothing billed
            _context.BillingPayment.Add(new BillingPayment
            {
                PaymentId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EncounterId = _encounterId,
                PaymentType = "ADVANCE",
                Amount = 1000,
                PaidAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();

            // Refund only part of the available 1000 credit — used to require admin approval.
            var response = await _handler.Handle(new AddPaymentEventRequestModel
            {
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EncounterId = _encounterId,
                Payment = new PaymentDetail { PaymentType = "REFUND", PaymentMode = "CASH", Amount = 400 },
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.PendingApproval, Is.Not.True);
            Assert.That(_context.CreditApproval.Count(), Is.EqualTo(0));

            var refund = _context.BillingPayment.Single(p => p.PaymentType == "REFUND");
            Assert.That(refund.Amount, Is.EqualTo(400));
        }

        [Test]
        public async Task Handle_Refund_DoesNotCreateBillingPaymentAllocation()
        {
            // Charge net 500, pay 700 (500 allocated to the charge + 200 held as unallocated
            // credit) — same shape as Handle_Advance_ExceedingDue_HoldsExcessAsCredit_NoApprovalNeeded.
            SeedInvoiceWithCharge(net: 500);
            var advanceResponse = await _handler.Handle(new AddPaymentEventRequestModel
            {
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EncounterId = _encounterId,
                Payment = new PaymentDetail { PaymentType = "ADVANCE", PaymentMode = "CASH", Amount = 700 },
            }, CancellationToken.None);
            Assert.That(advanceResponse.Success, Is.True);

            var allocationBeforeRefund = _context.BillingPaymentAllocation.Single();
            Assert.That(allocationBeforeRefund.AllocatedAmount, Is.EqualTo(500));
            var allocationChargeCountBeforeRefund = _context.BillingPaymentAllocationCharge.Count();
            Assert.That(allocationChargeCountBeforeRefund, Is.EqualTo(1));

            // Refund part of the 200 credit.
            var refundResponse = await _handler.Handle(new AddPaymentEventRequestModel
            {
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EncounterId = _encounterId,
                Payment = new PaymentDetail { PaymentType = "REFUND", PaymentMode = "CASH", Amount = 150 },
            }, CancellationToken.None);
            Assert.That(refundResponse.Success, Is.True);

            // A refund must NEVER create its own BillingPaymentAllocation/AllocationCharge (it's
            // money paid back OUT, not applied toward a charge) — and the original ADVANCE's
            // allocation must be completely untouched by it.
            Assert.That(_context.BillingPaymentAllocation.Count(), Is.EqualTo(1),
                "A refund must not create a new BillingPaymentAllocation row.");
            Assert.That(_context.BillingPaymentAllocation.Single().AllocatedAmount, Is.EqualTo(500),
                "The original payment's allocation must be unaffected by a later refund.");
            Assert.That(_context.BillingPaymentAllocationCharge.Count(), Is.EqualTo(allocationChargeCountBeforeRefund),
                "A refund must not create any BillingPaymentAllocationCharge rows.");
        }

        [Test]
        public async Task Handle_EncounterBelongsToAnotherHospital_ReturnsNotFound_DoesNotPost()
        {
            // Regression guard: this handler previously went straight to loading/creating an
            // invoice for request.EncounterId with NO ownership check at all -- unlike
            // AddChargeEventHandler, which has always validated Encounter+HospitalId+PatientId
            // first. HospitalAccessFilter only proves the caller belongs to their OWN
            // request.HospitalId; it says nothing about whether request.EncounterId belongs to
            // that hospital. Without this check, a billing user at hospital A who knows (or
            // enumerates) hospital B's EncounterId could post a real PAYMENT/ADVANCE/REFUND
            // against hospital B's bill, tagged as if it were hospital A's own transaction.
            var otherHospitalId = Guid.NewGuid();
            var otherEncounterId = Guid.NewGuid();
            _context.Encounter.Add(new Encounter
            {
                EncounterId = otherEncounterId,
                HospitalId = otherHospitalId, // belongs to a DIFFERENT hospital
                PatientId = "PT001",
                StatusCode = "OPEN",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            var victimInvoice = new BillingInvoice
            {
                InvoiceId = Guid.NewGuid(),
                HospitalId = otherHospitalId,
                EncounterId = otherEncounterId,
                PatientId = "PT001",
                InvoiceNo = "INV-VICTIM",
                InvoiceDate = DateTime.UtcNow,
                StatusCode = BillingConstants.InvoiceStatus.Draft,
                GrossAmount = 500,
                DiscountAmount = 0,
                NetAmount = 500,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            _context.BillingInvoice.Add(victimInvoice);
            await _context.SaveChangesAsync();

            // Attacker calls with THEIR OWN hospitalId but the victim's EncounterId.
            var response = await _handler.Handle(new AddPaymentEventRequestModel
            {
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EncounterId = otherEncounterId,
                Payment = new PaymentDetail { PaymentType = "PAYMENT", PaymentMode = "CASH", Amount = 100 },
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(_context.BillingPayment.Any(), Is.False, "No payment should ever be persisted for another hospital's encounter.");
            Assert.That(_context.BillingPaymentAllocation.Any(), Is.False);

            var reloadedVictimInvoice = _context.BillingInvoice.Single(bi => bi.InvoiceId == victimInvoice.InvoiceId);
            Assert.That(reloadedVictimInvoice.NetAmount, Is.EqualTo(500), "The victim's invoice must be completely untouched.");
        }

        [Test]
        public async Task Handle_Refund_ExceedsAvailableCredit_StillRejected()
        {
            SeedInvoiceWithCharge(net: 500); // no credit available (fully due, nothing collected)

            var response = await _handler.Handle(new AddPaymentEventRequestModel
            {
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EncounterId = _encounterId,
                Payment = new PaymentDetail { PaymentType = "REFUND", PaymentMode = "CASH", Amount = 100 },
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("credit"));
        }
    }
}
