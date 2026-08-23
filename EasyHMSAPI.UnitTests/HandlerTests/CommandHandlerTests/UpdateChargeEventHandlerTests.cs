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
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class UpdateChargeEventHandlerTests
    {
        private AppDbContext _context = null!;
        private UpdateChargeEventHandler _handler = null!;
        private Guid _hospitalId;
        private Guid _encounterId;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new UpdateChargeEventHandler(_context);
            _hospitalId = Guid.NewGuid();
            _encounterId = Guid.NewGuid();
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private BillingChargeEvent SeedCharge(decimal qty = 2, decimal rate = 500, decimal discount = 0, string status = "POSTED")
        {
            var charge = new BillingChargeEvent
            {
                ChargeEventId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EncounterId = _encounterId,
                DisplayName = "X-Ray",
                CategoryCode = "RADIOLOGY",
                Qty = qty,
                UnitPrice = rate,
                GrossAmount = qty * rate,
                DiscountAmount = discount,
                NetAmount = qty * rate - discount,
                StatusCode = status,
                ServiceDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            _context.BillingChargeEvent.Add(charge);
            _context.SaveChanges();
            return charge;
        }

        private BillingInvoice LinkToDraftInvoice(BillingChargeEvent charge, string status = "DRAFT")
        {
            var invoice = new BillingInvoice
            {
                InvoiceId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                EncounterId = _encounterId,
                PatientId = "PT001",
                InvoiceNo = "INV-1",
                InvoiceDate = DateTime.UtcNow,
                StatusCode = status,
                GrossAmount = charge.GrossAmount,
                DiscountAmount = charge.DiscountAmount,
                NetAmount = charge.NetAmount,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            _context.BillingInvoice.Add(invoice);
            _context.BillingInvoiceChargeEvent.Add(new BillingInvoiceChargeEvent { InvoiceId = invoice.InvoiceId, ChargeEventId = charge.ChargeEventId });
            _context.SaveChanges();
            return invoice;
        }

        [Test]
        public async Task Handle_UpdatesCharge_AndRecomputesInvoiceTotals()
        {
            var charge = SeedCharge(qty: 2, rate: 500);
            var invoice = LinkToDraftInvoice(charge);

            var response = await _handler.Handle(new UpdateChargeEventRequestModel
            {
                HospitalId = _hospitalId,
                ChargeEventId = charge.ChargeEventId,
                Qty = 3,
                Rate = 400,
                DiscountPercent = 10,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.Data!.Charge!.Qty, Is.EqualTo(3));
            Assert.That(response.Data.Charge.NetAmount, Is.EqualTo(1080)); // 3*400=1200, -10% = 1080

            var updatedInvoice = _context.BillingInvoice.First(i => i.InvoiceId == invoice.InvoiceId);
            Assert.That(updatedInvoice.NetAmount, Is.EqualTo(1080));
        }

        [Test]
        public async Task Handle_NoAdminApprovalNeeded_EvenWhenDiscountIsLarge()
        {
            var charge = SeedCharge(qty: 1, rate: 1000);

            var response = await _handler.Handle(new UpdateChargeEventRequestModel
            {
                HospitalId = _hospitalId,
                ChargeEventId = charge.ChargeEventId,
                Qty = 1,
                Rate = 1000,
                DiscountPercent = 90, // far beyond any typical cap
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.Data!.Charge!.NetAmount, Is.EqualTo(100));
            Assert.That(response.Data.Charge.DiscountApprovalRequired, Is.False);
        }

        [Test]
        public async Task Handle_RejectsEdit_WhenInvoiceFinalized()
        {
            var charge = SeedCharge();
            LinkToDraftInvoice(charge, status: BillingConstants.InvoiceStatus.Finalized);

            var response = await _handler.Handle(new UpdateChargeEventRequestModel
            {
                HospitalId = _hospitalId,
                ChargeEventId = charge.ChargeEventId,
                Qty = 5,
                Rate = 500,
                DiscountPercent = 0,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("finalized"));
        }

        [Test]
        public async Task Handle_RejectsEdit_WhenChargeBelongsToAClosedAdmissionDayBill()
        {
            // Regression guard: a closed interim bill is already printed/handed to the patient or
            // TPA. The invoice's own FINALIZED status doesn't cover this -- day-wise closes happen
            // mid-stay, well before discharge/finalize -- so before this fix, editing this exact
            // charge here would silently make the printed interim bill and the live ledger
            // disagree, with nothing in the system catching it.
            var charge = SeedCharge(qty: 1, rate: 1000); // net = 1000

            var dayBill = new AdmissionDayBill
            {
                AdmissionDayBillId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                EncounterId = _encounterId,
                PatientId = "PT001",
                DayNumber = 1,
                FromUtc = DateTime.UtcNow.AddDays(-1),
                ToUtc = DateTime.UtcNow,
                InterimBillNo = "IB-1",
                StatusCode = BillingConstants.DayBillStatus.Closed,
                ClosedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            _context.AdmissionDayBill.Add(dayBill);
            _context.AdmissionDayBillLine.Add(new AdmissionDayBillLine
            {
                AdmissionDayBillLineId = Guid.NewGuid(),
                AdmissionDayBillId = dayBill.AdmissionDayBillId,
                HospitalId = _hospitalId,
                ChargeEventId = charge.ChargeEventId,
                DisplayName = "X-Ray",
                ServiceDate = DateTime.UtcNow,
                Qty = 1,
                UnitPrice = 1000,
                GrossAmount = 1000,
                NetAmount = 1000,
                CreatedAt = DateTime.UtcNow,
            });
            _context.SaveChanges();

            var response = await _handler.Handle(new UpdateChargeEventRequestModel
            {
                HospitalId = _hospitalId,
                ChargeEventId = charge.ChargeEventId,
                Qty = 5,
                Rate = 1000,
                DiscountPercent = 0,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("Day 1"));
            Assert.That(response.Message, Does.Contain("IB-1"));
            var reloaded = _context.BillingChargeEvent.Single(c => c.ChargeEventId == charge.ChargeEventId);
            Assert.That(reloaded.Qty, Is.EqualTo(1), "The charge must be completely untouched.");
        }

        [Test]
        public async Task Handle_AllowsEdit_WhenAdmissionDayBillWasReopened()
        {
            // ReopenAdmissionDayHandler deletes the AdmissionDayBillLine rows for a reopened day --
            // this proves the lock releases correctly once that happens, not just that it engages.
            var charge = SeedCharge(qty: 1, rate: 1000);

            var dayBill = new AdmissionDayBill
            {
                AdmissionDayBillId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                EncounterId = _encounterId,
                PatientId = "PT001",
                DayNumber = 1,
                FromUtc = DateTime.UtcNow.AddDays(-1),
                ToUtc = DateTime.UtcNow,
                InterimBillNo = "IB-1",
                StatusCode = BillingConstants.DayBillStatus.Reopened, // already reopened; no lines exist
                ClosedAt = DateTime.UtcNow,
                ReopenedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            _context.AdmissionDayBill.Add(dayBill);
            // No AdmissionDayBillLine seeded -- ReopenAdmissionDayHandler removes it on reopen.
            _context.SaveChanges();

            var response = await _handler.Handle(new UpdateChargeEventRequestModel
            {
                HospitalId = _hospitalId,
                ChargeEventId = charge.ChargeEventId,
                Qty = 5,
                Rate = 1000,
                DiscountPercent = 0,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
        }

        [Test]
        public async Task Handle_RejectsReduction_BelowAlreadyPaidAmount()
        {
            var charge = SeedCharge(qty: 2, rate: 500); // net = 1000
            var invoice = LinkToDraftInvoice(charge);

            var payment = new BillingPayment
            {
                PaymentId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EncounterId = _encounterId,
                PaymentType = "PAYMENT",
                Amount = 800,
                PaidAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            _context.BillingPayment.Add(payment);
            var allocation = new BillingPaymentAllocation
            {
                AllocationId = Guid.NewGuid(),
                EncounterId = _encounterId,
                PaymentId = payment.PaymentId,
                InvoiceId = invoice.InvoiceId,
                AllocatedAmount = 800,
                CreatedAt = DateTime.UtcNow,
            };
            _context.BillingPaymentAllocation.Add(allocation);
            _context.BillingPaymentAllocationCharge.Add(new BillingPaymentAllocationCharge
            {
                AllocationChargeId = Guid.NewGuid(),
                AllocationId = allocation.AllocationId,
                ChargeEventId = charge.ChargeEventId,
                Amount = 800,
                CreatedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();

            // Try to reduce net amount to 500 — below the 800 already paid against this charge.
            var response = await _handler.Handle(new UpdateChargeEventRequestModel
            {
                HospitalId = _hospitalId,
                ChargeEventId = charge.ChargeEventId,
                Qty = 1,
                Rate = 500,
                DiscountPercent = 0,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("800"));
        }

        [Test]
        public async Task Handle_PreservesInvoiceLevelDiscount_WhenEditingAChargeThatHasNoLineDiscount()
        {
            var charge = SeedCharge(qty: 2, rate: 500, discount: 0); // gross = net = 1000
            var invoice = new BillingInvoice
            {
                InvoiceId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                EncounterId = _encounterId,
                PatientId = "PT001",
                InvoiceNo = "INV-1",
                InvoiceDate = DateTime.UtcNow,
                StatusCode = BillingConstants.InvoiceStatus.Draft,
                GrossAmount = charge.GrossAmount,
                // An overall "Add Discount" applied on top of the (line-discount-free) charge —
                // mirrors CreateDraftInvoiceHandler's invoiceLevelDiscount having been baked in here.
                DiscountAmount = 100,
                NetAmount = (charge.GrossAmount ?? 0) - 100,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            _context.BillingInvoice.Add(invoice);
            _context.BillingInvoiceChargeEvent.Add(new BillingInvoiceChargeEvent { InvoiceId = invoice.InvoiceId, ChargeEventId = charge.ChargeEventId });
            await _context.SaveChangesAsync();

            // Edit only the quantity — no discount change on the line itself — should NOT wipe the
            // invoice's overall discount (the bug this test guards against).
            var response = await _handler.Handle(new UpdateChargeEventRequestModel
            {
                HospitalId = _hospitalId,
                ChargeEventId = charge.ChargeEventId,
                Qty = 3,
                Rate = 500,
                DiscountPercent = 0,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True);

            var updatedInvoice = _context.BillingInvoice.First(i => i.InvoiceId == invoice.InvoiceId);
            // New gross = 3*500 = 1500; the pre-existing 100 overall discount must still apply.
            Assert.That(updatedInvoice.DiscountAmount, Is.EqualTo(100), "Editing a charge must not silently drop the invoice-level discount.");
            Assert.That(updatedInvoice.NetAmount, Is.EqualTo(1400));
        }

        [Test]
        public async Task Handle_ChargeNotFound_ReturnsFailure()
        {
            var response = await _handler.Handle(new UpdateChargeEventRequestModel
            {
                HospitalId = _hospitalId,
                ChargeEventId = Guid.NewGuid(),
                Qty = 1,
                Rate = 100,
                DiscountPercent = 0,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
        }
    }
}
