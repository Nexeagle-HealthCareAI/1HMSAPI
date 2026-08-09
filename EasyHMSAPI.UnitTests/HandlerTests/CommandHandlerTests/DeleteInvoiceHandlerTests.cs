using System;
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
    public class DeleteInvoiceHandlerTests
    {
        private AppDbContext _context = null!;
        private DeleteInvoiceHandler _handler = null!;
        private Guid _hospitalId;
        private Guid _encounterId;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new DeleteInvoiceHandler(_context);
            _hospitalId = Guid.NewGuid();
            _encounterId = Guid.NewGuid();
        }

        [TearDown]
        public void TearDown()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        private BillingInvoice SeedInvoice(string statusCode)
        {
            var invoice = new BillingInvoice
            {
                InvoiceId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                EncounterId = _encounterId,
                PatientId = "PT001",
                InvoiceNo = "INV-1",
                InvoiceDate = DateTime.UtcNow,
                StatusCode = statusCode,
                GrossAmount = 1000,
                NetAmount = 1000,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            _context.BillingInvoice.Add(invoice);
            return invoice;
        }

        [Test]
        public async Task Handle_MissingReason_ReturnsError()
        {
            var invoice = SeedInvoice("DRAFT");
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new DeleteInvoiceRequestModel { HospitalId = _hospitalId, EncounterId = _encounterId, Reason = "" }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("reason"));
        }

        [Test]
        public async Task Handle_InvoiceNotFound_ReturnsError()
        {
            var response = await _handler.Handle(new DeleteInvoiceRequestModel { HospitalId = _hospitalId, EncounterId = _encounterId, Reason = "Test" }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("not found"));
        }

        [Test]
        public async Task Handle_AlreadyCancelled_ReturnsError()
        {
            SeedInvoice("CANCELLED");
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new DeleteInvoiceRequestModel { HospitalId = _hospitalId, EncounterId = _encounterId, Reason = "Test" }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("already"));
        }

        [Test]
        public async Task Handle_FinalizedInvoice_DeletesAnyway_VoidsCharges_CancelsIncentive_UnallocatesPayment()
        {
            var invoice = SeedInvoice("FINALIZED");

            var doctorId = Guid.NewGuid();
            var charge = new BillingChargeEvent
            {
                ChargeEventId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EncounterId = _encounterId,
                DisplayName = "Consult",
                Qty = 1,
                UnitPrice = 1000,
                GrossAmount = 1000,
                NetAmount = 1000,
                StatusCode = "INVOICED",
                ServiceDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            _context.BillingChargeEvent.Add(charge);
            _context.BillingInvoiceChargeEvent.Add(new BillingInvoiceChargeEvent { InvoiceId = invoice.InvoiceId, ChargeEventId = charge.ChargeEventId });

            var ledgerEntry = new ConsultantIncentiveLedger
            {
                ConsultantIncentiveLedgerId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                DoctorId = doctorId,
                PatientId = "PT001",
                EncounterId = _encounterId,
                ChargeEventId = charge.ChargeEventId,
                IncentiveAmount = 50,
                StatusCode = "ACCRUED",
                AccruedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            _context.ConsultantIncentiveLedger.Add(ledgerEntry);

            var payment = new BillingPayment
            {
                PaymentId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EncounterId = _encounterId,
                PaymentType = "PAYMENT",
                Amount = 1000,
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
                AllocatedAmount = 1000,
                CreatedAt = DateTime.UtcNow,
            };
            _context.BillingPaymentAllocation.Add(allocation);
            _context.BillingPaymentAllocationCharge.Add(new BillingPaymentAllocationCharge
            {
                AllocationChargeId = Guid.NewGuid(),
                AllocationId = allocation.AllocationId,
                ChargeEventId = charge.ChargeEventId,
                Amount = 1000,
                CreatedAt = DateTime.UtcNow,
            });

            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new DeleteInvoiceRequestModel
            {
                HospitalId = _hospitalId,
                EncounterId = _encounterId,
                Reason = "Wrong patient billed",
                LoggedInUserName = "cashier1",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.ChargesVoided, Is.EqualTo(1));

            var reloadedInvoice = _context.BillingInvoice.First(i => i.InvoiceId == invoice.InvoiceId);
            Assert.That(reloadedInvoice.StatusCode, Is.EqualTo("CANCELLED"));
            Assert.That(reloadedInvoice.NetAmount, Is.EqualTo(0));
            Assert.That(reloadedInvoice.CancelReason, Is.EqualTo("Wrong patient billed"));

            var reloadedCharge = _context.BillingChargeEvent.First(c => c.ChargeEventId == charge.ChargeEventId);
            Assert.That(reloadedCharge.StatusCode, Is.EqualTo("VOID"));

            var reloadedLedger = _context.ConsultantIncentiveLedger.First(l => l.ConsultantIncentiveLedgerId == ledgerEntry.ConsultantIncentiveLedgerId);
            Assert.That(reloadedLedger.StatusCode, Is.EqualTo("CANCELLED"));

            Assert.That(_context.BillingPaymentAllocation.Any(a => a.AllocationId == allocation.AllocationId), Is.False);
            Assert.That(_context.BillingPaymentAllocationCharge.Any(ac => ac.AllocationId == allocation.AllocationId), Is.False);
            // The payment itself (the cash movement) is untouched, only its allocation is reversed.
            Assert.That(_context.BillingPayment.Any(p => p.PaymentId == payment.PaymentId), Is.True);
        }
    }
}
