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
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private BillingInvoice SeedInvoice(string statusCode, DateTime? cancelledAt = null)
        {
            var invoice = new BillingInvoice
            {
                InvoiceId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EncounterId = _encounterId,
                InvoiceNo = $"INV-{statusCode}",
                InvoiceDate = DateTime.UtcNow,
                StatusCode = statusCode,
                CancelledAt = cancelledAt,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            _context.BillingInvoice.Add(invoice);
            _context.SaveChanges();
            return invoice;
        }

        [Test]
        public async Task Handle_DeletesSpecificInvoice_VoidsItsLinkedCharges()
        {
            var invoice = SeedInvoice(BillingConstants.InvoiceStatus.Draft);
            var charge = new BillingChargeEvent
            {
                ChargeEventId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EncounterId = _encounterId,
                DisplayName = "Procedure",
                Qty = 1,
                UnitPrice = 500,
                NetAmount = 500,
                StatusCode = BillingConstants.ChargeEventStatus.Posted,
                ServiceDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            _context.BillingChargeEvent.Add(charge);
            _context.BillingInvoiceChargeEvent.Add(new BillingInvoiceChargeEvent { InvoiceId = invoice.InvoiceId, ChargeEventId = charge.ChargeEventId });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new DeleteInvoiceRequestModel
            {
                HospitalId = _hospitalId,
                EncounterId = _encounterId,
                InvoiceId = invoice.InvoiceId,
                Reason = "Test delete",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.ChargesVoided, Is.EqualTo(1));

            var reloadedInvoice = _context.BillingInvoice.Single(i => i.InvoiceId == invoice.InvoiceId);
            Assert.That(reloadedInvoice.StatusCode, Is.EqualTo(BillingConstants.InvoiceStatus.Cancelled));

            var reloadedCharge = _context.BillingChargeEvent.Single(c => c.ChargeEventId == charge.ChargeEventId);
            Assert.That(reloadedCharge.StatusCode, Is.EqualTo(BillingConstants.ChargeEventStatus.Void));
        }

        [Test]
        public async Task Handle_TargetsCorrectInvoice_WhenEncounterHasMultipleInvoices()
        {
            // Regression guard: before this fix, DeleteInvoiceRequestModel had no InvoiceId at all
            // -- the handler matched "the" invoice for an encounter via an unordered
            // FirstOrDefaultAsync(), which is ambiguous once an encounter has more than one
            // BillingInvoice row (delete one, keep billing, a fresh draft appears later). A second
            // delete call could silently hit the already-cancelled row and leave the real current
            // invoice untouched.
            var oldCancelledAt = DateTime.UtcNow.AddDays(-1);
            var oldInvoice = SeedInvoice(BillingConstants.InvoiceStatus.Cancelled, cancelledAt: oldCancelledAt);
            var currentInvoice = SeedInvoice(BillingConstants.InvoiceStatus.Draft);

            var response = await _handler.Handle(new DeleteInvoiceRequestModel
            {
                HospitalId = _hospitalId,
                EncounterId = _encounterId,
                InvoiceId = currentInvoice.InvoiceId,
                Reason = "Correcting the current draft",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True);

            var reloadedCurrent = _context.BillingInvoice.Single(i => i.InvoiceId == currentInvoice.InvoiceId);
            Assert.That(reloadedCurrent.StatusCode, Is.EqualTo(BillingConstants.InvoiceStatus.Cancelled));
            Assert.That(reloadedCurrent.CancelReason, Is.EqualTo("Correcting the current draft"));

            // The already-cancelled row must be completely untouched by this call.
            var reloadedOld = _context.BillingInvoice.Single(i => i.InvoiceId == oldInvoice.InvoiceId);
            Assert.That(reloadedOld.CancelledAt, Is.EqualTo(oldCancelledAt));
            Assert.That(reloadedOld.CancelReason, Is.Null);
        }

        [Test]
        public async Task Handle_RejectsDelete_WhenInvoiceBelongsToAnotherEncounter()
        {
            var invoice = SeedInvoice(BillingConstants.InvoiceStatus.Draft);
            var otherEncounterId = Guid.NewGuid();

            var response = await _handler.Handle(new DeleteInvoiceRequestModel
            {
                HospitalId = _hospitalId,
                EncounterId = otherEncounterId,
                InvoiceId = invoice.InvoiceId,
                Reason = "Attempted cross-encounter delete",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("not found"));

            var reloaded = _context.BillingInvoice.Single(i => i.InvoiceId == invoice.InvoiceId);
            Assert.That(reloaded.StatusCode, Is.EqualTo(BillingConstants.InvoiceStatus.Draft), "The invoice must be completely untouched.");
        }

        [Test]
        public async Task Handle_RejectsDelete_WhenInvoiceBelongsToAnotherHospital()
        {
            var invoice = SeedInvoice(BillingConstants.InvoiceStatus.Draft);
            var otherHospitalId = Guid.NewGuid();

            var response = await _handler.Handle(new DeleteInvoiceRequestModel
            {
                HospitalId = otherHospitalId,
                EncounterId = _encounterId,
                InvoiceId = invoice.InvoiceId,
                Reason = "Attempted cross-hospital delete",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            var reloaded = _context.BillingInvoice.Single(i => i.InvoiceId == invoice.InvoiceId);
            Assert.That(reloaded.StatusCode, Is.EqualTo(BillingConstants.InvoiceStatus.Draft));
        }

        [Test]
        public async Task Handle_RejectsDelete_WhenAlreadyCancelled()
        {
            var invoice = SeedInvoice(BillingConstants.InvoiceStatus.Cancelled, cancelledAt: DateTime.UtcNow);

            var response = await _handler.Handle(new DeleteInvoiceRequestModel
            {
                HospitalId = _hospitalId,
                EncounterId = _encounterId,
                InvoiceId = invoice.InvoiceId,
                Reason = "Try again",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("already"));
        }

        [Test]
        public async Task Handle_RejectsDelete_WhenReasonMissing()
        {
            var invoice = SeedInvoice(BillingConstants.InvoiceStatus.Draft);

            var response = await _handler.Handle(new DeleteInvoiceRequestModel
            {
                HospitalId = _hospitalId,
                EncounterId = _encounterId,
                InvoiceId = invoice.InvoiceId,
                Reason = "",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("reason"));
        }

        [Test]
        public async Task Handle_RejectsDelete_WhenInvoiceIdMissing()
        {
            var response = await _handler.Handle(new DeleteInvoiceRequestModel
            {
                HospitalId = _hospitalId,
                EncounterId = _encounterId,
                InvoiceId = Guid.Empty,
                Reason = "No invoice id",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
        }
    }
}
