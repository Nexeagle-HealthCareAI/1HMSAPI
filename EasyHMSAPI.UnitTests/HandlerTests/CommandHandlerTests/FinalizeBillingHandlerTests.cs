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
    public class FinalizeBillingHandlerTests
    {
        private AppDbContext _context = null!;
        private FinalizeBillingHandler _handler = null!;
        private Guid _hospitalId;
        private Guid _encounterId;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new FinalizeBillingHandler(_context);
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
        public async Task Handle_Finalize_SucceedsEvenWithPendingDiscountApproval()
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
                DiscountAmount = 500,
                NetAmount = 500,
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
                DiscountAmount = 500,
                NetAmount = 500,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            _context.BillingInvoice.Add(invoice);
            _context.BillingInvoiceChargeEvent.Add(new BillingInvoiceChargeEvent { InvoiceId = invoice.InvoiceId, ChargeEventId = charge.ChargeEventId });

            // A leftover PENDING discount approval from before the approval workflow was removed —
            // finalize must no longer be blocked by this (historical or otherwise).
            _context.DiscountApproval.Add(new DiscountApproval
            {
                DiscountApprovalId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                ChargeEventId = charge.ChargeEventId,
                PatientId = "PT001",
                EncounterId = _encounterId,
                GrossAmount = 1000,
                RequestedDiscountPercent = 50,
                RequestedDiscountAmount = 500,
                CapPercent = 20,
                OverByPercent = 30,
                Status = "PENDING",
                RequestedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new FinalizeBillingRequestModel
            {
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EncounterId = _encounterId,
                Type = "finalize",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.Message, Does.Contain("finalized"));

            var updatedInvoice = _context.BillingInvoice.Single(i => i.InvoiceId == invoice.InvoiceId);
            Assert.That(updatedInvoice.StatusCode, Is.EqualTo(BillingConstants.InvoiceStatus.Finalized));
        }
    }
}
