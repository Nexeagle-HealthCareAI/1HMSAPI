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
    public class DeleteBillingEventHandlerTests
    {
        private AppDbContext _context = null!;
        private DeleteBillingEventHandler _handler = null!;
        private Guid _hospitalId;
        private Guid _encounterId;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new DeleteBillingEventHandler(_context);
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
        public async Task Handle_DeletesCharge_Immediately_NoReasonRequired_NoApproval()
        {
            var charge = new BillingChargeEvent
            {
                ChargeEventId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EncounterId = _encounterId,
                DisplayName = "Consult",
                Qty = 1,
                UnitPrice = 300,
                GrossAmount = 300,
                NetAmount = 300,
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
                StatusCode = "DRAFT",
                GrossAmount = 300,
                NetAmount = 300,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            _context.BillingInvoice.Add(invoice);
            _context.BillingInvoiceChargeEvent.Add(new BillingInvoiceChargeEvent { InvoiceId = invoice.InvoiceId, ChargeEventId = charge.ChargeEventId });
            await _context.SaveChangesAsync();

            // No Reason supplied — used to be rejected outright before even reaching approval.
            var response = await _handler.Handle(new DeleteBillingEventRequestModel
            {
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EventId = charge.ChargeEventId,
                Type = "Charges",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.PendingApproval, Is.Not.True);
            Assert.That(_context.BillingChargeEvent.Any(c => c.ChargeEventId == charge.ChargeEventId), Is.False);
            Assert.That(_context.CreditApproval.Count(), Is.EqualTo(0));
        }

        [Test]
        public async Task Handle_DeletesPayment_Immediately_NoApproval()
        {
            var payment = new BillingPayment
            {
                PaymentId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EncounterId = _encounterId,
                PaymentType = "PAYMENT",
                Amount = 200,
                PaidAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            _context.BillingPayment.Add(payment);
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new DeleteBillingEventRequestModel
            {
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EventId = payment.PaymentId,
                Type = "Payment",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(_context.BillingPayment.Any(p => p.PaymentId == payment.PaymentId), Is.False);
            Assert.That(_context.CreditApproval.Count(), Is.EqualTo(0));
        }
    }
}
