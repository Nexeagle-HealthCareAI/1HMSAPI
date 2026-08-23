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
        public async Task Handle_RejectsDelete_WhenChargeBelongsToAClosedAdmissionDayBill()
        {
            // Same day-lock regression guard as UpdateChargeEventHandlerTests -- see its comment.
            // Deliberately never linked to a BillingInvoice, so this hits the "charge never linked
            // to an invoice" branch that would otherwise delete it outright with no lock check at
            // all if the day-bill check weren't ordered before it.
            var charge = new BillingChargeEvent
            {
                ChargeEventId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EncounterId = _encounterId,
                DisplayName = "X-Ray",
                Qty = 1,
                UnitPrice = 1000,
                GrossAmount = 1000,
                NetAmount = 1000,
                StatusCode = "POSTED",
                ServiceDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            _context.BillingChargeEvent.Add(charge);

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
                StatusCode = "CLOSED",
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
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new DeleteBillingEventRequestModel
            {
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EventId = charge.ChargeEventId,
                Type = "Charges",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("Day 1"));
            Assert.That(_context.BillingChargeEvent.Any(c => c.ChargeEventId == charge.ChargeEventId), Is.True);
        }

        [Test]
        public async Task Handle_ChargeNeverLinkedToAnInvoice_DeletesDirectlyInsteadOfFailing()
        {
            // Posted but the auto-createDraftInvoice step never ran (e.g. BillingPage's best-effort
            // call failed silently) -- no BillingInvoiceChargeEvent row exists for this charge.
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
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new DeleteBillingEventRequestModel
            {
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EventId = charge.ChargeEventId,
                Type = "Charges",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(_context.BillingChargeEvent.Any(c => c.ChargeEventId == charge.ChargeEventId), Is.False);
        }

        [Test]
        public async Task Handle_DeletesLinkedCharge_CancelsAccruedIncentive()
        {
            var doctorId = Guid.NewGuid();
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
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new DeleteBillingEventRequestModel
            {
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EventId = charge.ChargeEventId,
                Type = "Charges",
                LoggedInUserName = "cashier1",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            var reloaded = _context.ConsultantIncentiveLedger.First(l => l.ConsultantIncentiveLedgerId == ledgerEntry.ConsultantIncentiveLedgerId);
            Assert.That(reloaded.StatusCode, Is.EqualTo("CANCELLED"));
            Assert.That(reloaded.CancelledAt, Is.Not.Null);
            Assert.That(reloaded.CancelledBy, Is.EqualTo("cashier1"));
        }

        [Test]
        public async Task Handle_DeletesLinkedCharge_NeverTouchesAnAlreadyPaidIncentive()
        {
            var doctorId = Guid.NewGuid();
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
            var ledgerEntry = new ConsultantIncentiveLedger
            {
                ConsultantIncentiveLedgerId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                DoctorId = doctorId,
                PatientId = "PT001",
                EncounterId = _encounterId,
                ChargeEventId = charge.ChargeEventId,
                IncentiveAmount = 50,
                StatusCode = "PAID",
                AccruedAt = DateTime.UtcNow,
                PaidAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            _context.ConsultantIncentiveLedger.Add(ledgerEntry);
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new DeleteBillingEventRequestModel
            {
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EventId = charge.ChargeEventId,
                Type = "Charges",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            var reloaded = _context.ConsultantIncentiveLedger.First(l => l.ConsultantIncentiveLedgerId == ledgerEntry.ConsultantIncentiveLedgerId);
            Assert.That(reloaded.StatusCode, Is.EqualTo("PAID"));
        }

        [Test]
        public async Task Handle_ChargeBelongsToAnotherHospital_ReturnsNotFound_DoesNotDelete()
        {
            // Regression guard: HospitalAccessFilter only proves the caller belongs to
            // request.HospitalId -- it says nothing about whether EventId itself belongs to that
            // hospital. Before the fix, this lookup had no HospitalId filter at all, so a billing
            // user at ANY hospital could delete/void a charge belonging to a DIFFERENT hospital
            // just by supplying its ChargeEventId.
            var otherHospitalId = Guid.NewGuid();
            var charge = new BillingChargeEvent
            {
                ChargeEventId = Guid.NewGuid(),
                HospitalId = otherHospitalId,
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
            await _context.SaveChangesAsync();

            // Attacker calls with THEIR OWN (different) hospitalId, but the victim's charge id.
            var response = await _handler.Handle(new DeleteBillingEventRequestModel
            {
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EventId = charge.ChargeEventId,
                Type = "Charges",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(_context.BillingChargeEvent.Any(c => c.ChargeEventId == charge.ChargeEventId), Is.True);
        }

        [Test]
        public async Task Handle_PaymentBelongsToAnotherHospital_ReturnsNotFound_DoesNotDelete()
        {
            var otherHospitalId = Guid.NewGuid();
            var payment = new BillingPayment
            {
                PaymentId = Guid.NewGuid(),
                HospitalId = otherHospitalId,
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

            Assert.That(response.Success, Is.False);
            Assert.That(_context.BillingPayment.Any(p => p.PaymentId == payment.PaymentId), Is.True);
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
