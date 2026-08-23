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
    public class CreateDraftInvoiceHandlerTests
    {
        private AppDbContext _context = null!;
        private CreateDraftInvoiceHandler _handler = null!;
        private Guid _hospitalId;
        private Guid _encounterId;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new CreateDraftInvoiceHandler(_context);
            _hospitalId = Guid.NewGuid();
            _encounterId = Guid.NewGuid();
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        [Test]
        public async Task Handle_DiscountBelowAlreadyCollectedMoney_AppliesDirectly_NoApprovalNeeded()
        {
            _context.Encounter.Add(new Encounter
            {
                EncounterId = _encounterId,
                HospitalId = _hospitalId,
                PatientId = "PT001",
                StatusCode = BillingConstants.EncounterStatus.Open,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });

            var charge = new BillingChargeEvent
            {
                ChargeEventId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EncounterId = _encounterId,
                DisplayName = "Procedure",
                Qty = 1,
                UnitPrice = 1000,
                GrossAmount = 1000,
                DiscountAmount = 0,
                NetAmount = 1000,
                StatusCode = BillingConstants.ChargeEventStatus.Posted,
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
                GrossAmount = 1000,
                DiscountAmount = 0,
                NetAmount = 1000,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            _context.BillingInvoice.Add(invoice);
            _context.BillingInvoiceChargeEvent.Add(new BillingInvoiceChargeEvent { InvoiceId = invoice.InvoiceId, ChargeEventId = charge.ChargeEventId });

            // Already 900 collected and allocated against this invoice.
            var payment = new BillingPayment
            {
                PaymentId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EncounterId = _encounterId,
                PaymentType = "PAYMENT",
                Amount = 900,
                PaidAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            _context.BillingPayment.Add(payment);
            _context.BillingPaymentAllocation.Add(new BillingPaymentAllocation
            {
                AllocationId = Guid.NewGuid(),
                EncounterId = _encounterId,
                PaymentId = payment.PaymentId,
                InvoiceId = invoice.InvoiceId,
                AllocatedAmount = 900,
                CreatedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();

            // A 200 discount brings NetAmount to 800 — below the 900 already collected.
            // This used to be blocked pending admin approval.
            var response = await _handler.Handle(new CreateDraftInvoiceRequestModel
            {
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EncounterId = _encounterId,
                InvoiceDiscountAmount = 200,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.PendingApproval, Is.Not.True, "Discount dipping into collected money must apply directly now.");
            Assert.That(_context.CreditApproval.Count(), Is.EqualTo(0));

            var updated = _context.BillingInvoice.Single(i => i.InvoiceId == invoice.InvoiceId);
            Assert.That(updated.NetAmount, Is.EqualTo(800));
            Assert.That(updated.DiscountAmount, Is.EqualTo(200));
        }

        [Test]
        public async Task Handle_ScalesGstBreakdown_ByInvoiceLevelDiscountRatio()
        {
            // Regression guard for the Medium-severity GST reconciliation bug: an invoice-level
            // discount applied at creation time (not tied to any one line) must shrink
            // Taxable/Cgst/Sgst/Tax by the same proportion it shrinks NetAmount -- otherwise the
            // printed invoice's tax section stops reconciling with the discounted grand total.
            _context.Encounter.Add(new Encounter
            {
                EncounterId = _encounterId,
                HospitalId = _hospitalId,
                PatientId = "PT001",
                StatusCode = BillingConstants.EncounterStatus.Open,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });

            var charge = new BillingChargeEvent
            {
                ChargeEventId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EncounterId = _encounterId,
                DisplayName = "Procedure",
                Qty = 1,
                UnitPrice = 1000,
                GrossAmount = 1000,
                DiscountAmount = 0,
                NetAmount = 1000,
                GstRate = 18,
                IsTaxInclusive = false,
                IsInterState = false,
                TaxableAmount = 1000,
                CgstAmount = 90,
                SgstAmount = 90,
                IgstAmount = 0,
                TaxAmount = 180,
                StatusCode = BillingConstants.ChargeEventStatus.Posted,
                ServiceDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            _context.BillingChargeEvent.Add(charge);
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new CreateDraftInvoiceRequestModel
            {
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EncounterId = _encounterId,
                InvoiceDiscountAmount = 100,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.Data!.NetAmount, Is.EqualTo(900));
            Assert.That(response.Data.TaxableAmount, Is.EqualTo(900), "Taxable must scale down with the invoice-level discount.");
            Assert.That(response.Data.CgstAmount, Is.EqualTo(81));
            Assert.That(response.Data.SgstAmount, Is.EqualTo(81));
            Assert.That(response.Data.TaxAmount, Is.EqualTo(162));

            var invoice = _context.BillingInvoice.Single(i => i.EncounterId == _encounterId);
            Assert.That(invoice.TaxableAmount, Is.EqualTo(900));
            Assert.That(invoice.CgstAmount, Is.EqualTo(81));
            Assert.That(invoice.SgstAmount, Is.EqualTo(81));
            Assert.That(invoice.TaxAmount, Is.EqualTo(162));
        }
    }
}
