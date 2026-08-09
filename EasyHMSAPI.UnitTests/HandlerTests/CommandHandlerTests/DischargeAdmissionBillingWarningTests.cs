using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using MediatR;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    // Discharge must never be blocked by billing state (matches the handler's own documented
    // posture for EXPIRED/LAMA/DAMA) -- these tests confirm the new outstanding-balance fields are
    // purely informational and discharge always succeeds regardless of what they say.
    [TestFixture]
    public class DischargeAdmissionBillingWarningTests
    {
        private AppDbContext _context = null!;
        private AdmissionStatusCommandHandlers _handler = null!;
        private Guid _hospitalId;
        private Guid _encounterId;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new AdmissionStatusCommandHandlers(
                _context, new Mock<ISmsService>().Object, new Mock<IWhatsAppMessagingService>().Object, new Mock<IMediator>().Object);
            _hospitalId = Guid.NewGuid();
            _encounterId = Guid.NewGuid();
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private Admission SeedActiveAdmission(Guid? encounterId)
        {
            var admission = new Admission
            {
                AdmissionId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                PatientId = "PT001",
                AdmissionNo = "ADM-1",
                EncounterId = encounterId,
                AdmittedAt = DateTime.UtcNow,
                StatusCode = "ADMITTED",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            _context.Admission.Add(admission);
            return admission;
        }

        [Test]
        public async Task Handle_NoEncounter_DischargesWithoutFlaggingAnyBillingWarning()
        {
            var admission = SeedActiveAdmission(null);
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new DischargeAdmissionRequestModel { HospitalId = _hospitalId, AdmissionId = admission.AdmissionId }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.HasOutstandingBalance, Is.False);
            Assert.That(response.HasUnfinalizedInvoice, Is.False);
        }

        [Test]
        public async Task Handle_FullyPaidEncounter_DischargesWithNoOutstandingBalance()
        {
            var admission = SeedActiveAdmission(_encounterId);
            _context.BillingChargeEvent.Add(new BillingChargeEvent
            {
                ChargeEventId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EncounterId = _encounterId,
                DisplayName = "Consult",
                Qty = 1,
                UnitPrice = 500,
                GrossAmount = 500,
                NetAmount = 500,
                StatusCode = "POSTED",
                ServiceDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            _context.BillingPayment.Add(new BillingPayment
            {
                PaymentId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EncounterId = _encounterId,
                PaymentType = "PAYMENT",
                Amount = 500,
                PaidAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new DischargeAdmissionRequestModel { HospitalId = _hospitalId, AdmissionId = admission.AdmissionId }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.HasOutstandingBalance, Is.False);
            Assert.That(response.OutstandingAmount, Is.EqualTo(0m));
        }

        [Test]
        public async Task Handle_UnpaidEncounter_StillDischarges_ButFlagsOutstandingBalance()
        {
            var admission = SeedActiveAdmission(_encounterId);
            _context.BillingChargeEvent.Add(new BillingChargeEvent
            {
                ChargeEventId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EncounterId = _encounterId,
                DisplayName = "Consult",
                Qty = 1,
                UnitPrice = 1200,
                GrossAmount = 1200,
                NetAmount = 1200,
                StatusCode = "POSTED",
                ServiceDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new DischargeAdmissionRequestModel { HospitalId = _hospitalId, AdmissionId = admission.AdmissionId }, CancellationToken.None);

            // The discharge itself must succeed regardless.
            Assert.That(response.Success, Is.True);
            Assert.That(response.HasOutstandingBalance, Is.True);
            Assert.That(response.OutstandingAmount, Is.EqualTo(1200m));
        }

        [Test]
        public async Task Handle_UnfinalizedInvoice_StillDischarges_ButFlagsIt()
        {
            var admission = SeedActiveAdmission(_encounterId);
            _context.BillingInvoice.Add(new BillingInvoice
            {
                InvoiceId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                EncounterId = _encounterId,
                PatientId = "PT001",
                InvoiceNo = "INV-1",
                InvoiceDate = DateTime.UtcNow,
                StatusCode = "DRAFT",
                GrossAmount = 0,
                NetAmount = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new DischargeAdmissionRequestModel { HospitalId = _hospitalId, AdmissionId = admission.AdmissionId }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.HasUnfinalizedInvoice, Is.True);
        }
    }
}
