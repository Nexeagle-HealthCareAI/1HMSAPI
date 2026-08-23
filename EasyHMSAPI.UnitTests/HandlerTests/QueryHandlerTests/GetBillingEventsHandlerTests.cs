using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class GetBillingEventsHandlerTests
    {
        private AppDbContext _context = null!;
        private GetBillingEventsHandler _handler = null!;
        private Guid _hospitalId;
        private Guid _encounterId;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetBillingEventsHandler(_context);
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
        public async Task Handle_ReturnsEveryInvoiceForTheEncounter_NewestFirst()
        {
            var older = new BillingInvoice
            {
                InvoiceId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EncounterId = _encounterId,
                InvoiceNo = "INV-OLD",
                InvoiceDate = DateTime.UtcNow.AddDays(-2),
                StatusCode = BillingConstants.InvoiceStatus.Cancelled,
                NetAmount = 500,
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                UpdatedAt = DateTime.UtcNow.AddDays(-2),
            };
            var current = new BillingInvoice
            {
                InvoiceId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EncounterId = _encounterId,
                InvoiceNo = "INV-CURRENT",
                InvoiceDate = DateTime.UtcNow,
                StatusCode = BillingConstants.InvoiceStatus.Draft,
                NetAmount = 800,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            _context.BillingInvoice.AddRange(older, current);
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetBillingEventsRequestModel
            {
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EncounterId = _encounterId,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.Data!.Invoices, Has.Count.EqualTo(2));
            Assert.That(response.Data.Invoices![0].InvoiceNo, Is.EqualTo("INV-CURRENT"), "Newest invoice must lead the list.");
            Assert.That(response.Data.Invoices![1].InvoiceNo, Is.EqualTo("INV-OLD"));
            Assert.That(response.Data.CurrentInvoice!.InvoiceNo, Is.EqualTo("INV-CURRENT"));
        }

        [Test]
        public async Task Handle_PaymentRow_UsesPaidAt_NotCreatedAt()
        {
            // On a backdated visit PaidAt (visit-date-derived) and CreatedAt (real audit time)
            // diverge -- the ledger must show PaidAt so a backdated visit's payment doesn't display
            // today's date.
            var paidAt = DateTime.UtcNow.Date.AddDays(-7);
            var createdAt = DateTime.UtcNow;
            _context.BillingPayment.Add(new BillingPayment
            {
                PaymentId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EncounterId = _encounterId,
                PaymentType = BillingConstants.PaymentType.Payment,
                Amount = 500,
                PaidAt = paidAt,
                CreatedAt = createdAt,
                UpdatedAt = createdAt,
            });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetBillingEventsRequestModel
            {
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EncounterId = _encounterId,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            var payment = response.Data!.Payments!.Single();
            Assert.That(payment.CreatedDateTime, Is.EqualTo(paidAt));
            Assert.That(payment.CreatedDateTime, Is.Not.EqualTo(createdAt));
        }
    }
}
