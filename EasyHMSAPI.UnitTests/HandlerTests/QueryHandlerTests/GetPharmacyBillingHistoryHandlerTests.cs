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
    public class GetPharmacyBillingHistoryHandlerTests
    {
        private AppDbContext _context = null!;
        private GetPharmacyBillingHistoryHandler _handler = null!;
        private Guid _hospitalId;
        private DateTime _today;

        [SetUp]
        public async Task SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetPharmacyBillingHistoryHandler(_context);
            _hospitalId = Guid.NewGuid();
            _today = DateTime.UtcNow.Date;

            await SeedPharmacyInvoice("INV-BH-0001", "PARA-CE", _today, "PHARMACY_COUNTER", "CASH", "cashier1", "PTID001", "Amir Yadav", 120m, 3m);
            await SeedPharmacyInvoice("INV-BH-0002", "AMOX-CE", _today.AddDays(-5), "PHARMACY_IPD", "UPI", "cashier2", "PTID002", "Asif Anwar", 60m, 2m);
            // Non-pharmacy invoice — must never appear.
            await SeedPharmacyInvoice("INV-BH-0003", "CONSULT-CE", _today, "OPD", "CASH", "cashier1", "PTID003", "Someone Else", 500m, 1m);

            await _context.SaveChangesAsync();
        }

        [TearDown]
        public void TearDown()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        private async Task SeedPharmacyInvoice(
            string invoiceNo, string tag, DateTime serviceDate, string sourceModule,
            string paymentMode, string createdBy, string patientId, string patientName,
            decimal netAmount, decimal qty)
        {
            var invoiceId = Guid.NewGuid();
            var encounterId = Guid.NewGuid();
            var chargeEventId = Guid.NewGuid();

            _context.PatientRegistrations.Add(new PatientRegistration
            {
                RegistrationId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                PatientId = patientId,
                FullName = patientName,
            });

            _context.BillingInvoice.Add(new BillingInvoice
            {
                InvoiceId = invoiceId,
                HospitalId = _hospitalId,
                EncounterId = encounterId,
                PatientId = patientId,
                InvoiceNo = invoiceNo,
                InvoiceDate = serviceDate,
                NetAmount = netAmount,
                StatusCode = BillingConstants.InvoiceStatus.Finalized,
                CreatedAt = serviceDate,
                CreatedBy = createdBy,
                UpdatedAt = serviceDate,
                UpdatedBy = createdBy,
            });

            _context.BillingChargeEvent.Add(new BillingChargeEvent
            {
                ChargeEventId = chargeEventId,
                HospitalId = _hospitalId,
                EncounterId = encounterId,
                DisplayName = tag,
                SourceModule = sourceModule,
                Qty = qty,
                UnitPrice = netAmount / qty,
                NetAmount = netAmount,
                StatusCode = BillingConstants.ChargeEventStatus.Posted,
                ServiceDate = serviceDate,
                CreatedAt = serviceDate,
                UpdatedAt = serviceDate,
            });

            _context.BillingInvoiceChargeEvent.Add(new BillingInvoiceChargeEvent
            {
                InvoiceId = invoiceId,
                ChargeEventId = chargeEventId,
            });

            _context.BillingPayment.Add(new BillingPayment
            {
                PaymentId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                EncounterId = encounterId,
                PatientId = patientId,
                PaymentType = BillingConstants.PaymentType.Payment,
                PaymentMode = paymentMode,
                Amount = netAmount,
                PaidAt = serviceDate,
                CreatedAt = serviceDate,
                UpdatedAt = serviceDate,
            });
        }

        [Test]
        public async Task Handle_NoDateFilter_ReturnsAllPharmacyBillsOnly()
        {
            var response = await _handler.Handle(new GetPharmacyBillingHistoryRequestModel { HospitalId = _hospitalId }, CancellationToken.None);

            Assert.That(response.TotalBills, Is.EqualTo(2));
            Assert.That(response.Bills.Select(b => b.InvoiceNo), Does.Contain("INV-BH-0001"));
            Assert.That(response.Bills.Select(b => b.InvoiceNo), Does.Contain("INV-BH-0002"));
            Assert.That(response.Bills.Select(b => b.InvoiceNo), Does.Not.Contain("INV-BH-0003"));
        }

        [Test]
        public async Task Handle_FromDateOnly_ExcludesOlderBills()
        {
            var response = await _handler.Handle(new GetPharmacyBillingHistoryRequestModel
            {
                HospitalId = _hospitalId,
                FromDate = _today,
            }, CancellationToken.None);

            Assert.That(response.TotalBills, Is.EqualTo(1));
            Assert.That(response.Bills.Single().InvoiceNo, Is.EqualTo("INV-BH-0001"));
        }

        [Test]
        public async Task Handle_ReturnsRowFieldsCorrectly()
        {
            var response = await _handler.Handle(new GetPharmacyBillingHistoryRequestModel
            {
                HospitalId = _hospitalId,
                FromDate = _today,
                ToDate = _today,
            }, CancellationToken.None);

            var row = response.Bills.Single();
            Assert.That(row.PatientName, Is.EqualTo("Amir Yadav"));
            Assert.That(row.SourceModule, Is.EqualTo("PHARMACY_COUNTER"));
            Assert.That(row.PaymentMode, Is.EqualTo("CASH"));
            Assert.That(row.ProcessedBy, Is.EqualTo("cashier1"));
            Assert.That(row.ItemCount, Is.EqualTo(1));
            Assert.That(row.TotalQty, Is.EqualTo(3));
            Assert.That(row.NetAmount, Is.EqualTo(120));
            Assert.That(response.TotalAmount, Is.EqualTo(120));
        }

        [Test]
        public async Task Handle_InvoiceWithProcessedReturn_SubtractsRefundFromNetSales()
        {
            // Regression test for a real audit finding: returns never adjusted the original
            // BillingChargeEvent/BillingInvoice, so a returned sale used to still show its full
            // original amount here with no sign of the refund -- net sales overstated.
            var invoiceId = Guid.NewGuid();
            var encounterId = Guid.NewGuid();
            var chargeEventId = Guid.NewGuid();

            _context.BillingInvoice.Add(new BillingInvoice
            {
                InvoiceId = invoiceId,
                HospitalId = _hospitalId,
                EncounterId = encounterId,
                PatientId = "PTID999",
                InvoiceNo = "INV-BH-RETURN",
                InvoiceDate = _today,
                NetAmount = 100m,
                StatusCode = BillingConstants.InvoiceStatus.Finalized,
                CreatedAt = _today,
                CreatedBy = "cashier1",
                UpdatedAt = _today,
                UpdatedBy = "cashier1",
            });
            _context.BillingChargeEvent.Add(new BillingChargeEvent
            {
                ChargeEventId = chargeEventId,
                HospitalId = _hospitalId,
                EncounterId = encounterId,
                DisplayName = "RETURNED-ITEM",
                SourceModule = "PHARMACY_COUNTER",
                Qty = 10,
                UnitPrice = 10,
                NetAmount = 100m,
                StatusCode = BillingConstants.ChargeEventStatus.Posted,
                ServiceDate = _today,
                CreatedAt = _today,
                UpdatedAt = _today,
            });
            _context.BillingInvoiceChargeEvent.Add(new BillingInvoiceChargeEvent { InvoiceId = invoiceId, ChargeEventId = chargeEventId });
            _context.PharmacyReturn.Add(new PharmacyReturn
            {
                ReturnId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                InvoiceId = invoiceId,
                InvoiceNo = "INV-BH-RETURN",
                PatientId = "PTID999",
                EncounterId = encounterId,
                ReturnNo = "PHRET-0001",
                TotalRefundAmount = 40m,
                ReturnedAt = _today,
                CreatedAt = _today,
            });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetPharmacyBillingHistoryRequestModel
            {
                HospitalId = _hospitalId,
                FromDate = _today,
                ToDate = _today,
            }, CancellationToken.None);

            var row = response.Bills.Single(b => b.InvoiceNo == "INV-BH-RETURN");
            Assert.That(row.NetAmount, Is.EqualTo(100m), "original charged amount is untouched");
            Assert.That(row.ReturnedAmount, Is.EqualTo(40m));
            Assert.That(response.TotalReturnedAmount, Is.EqualTo(40m));
            Assert.That(response.NetSalesAmount, Is.EqualTo(response.TotalAmount - 40m));
        }

        [Test]
        public async Task Handle_Pagination_TotalsReflectFullSetButBillsAreOnlyThePage()
        {
            // 3 extra invoices today, on top of INV-BH-0001 already seeded in SetUp -- 4 total for
            // "today" across this test.
            for (int i = 0; i < 3; i++)
            {
                await SeedPharmacyInvoice($"INV-BH-PAGE-{i}", $"TAG-{i}", _today, "PHARMACY_COUNTER", "CASH", "cashier1", $"PTIDPAGE{i}", $"Patient {i}", 50m, 1m);
            }
            await _context.SaveChangesAsync();

            var page1 = await _handler.Handle(new GetPharmacyBillingHistoryRequestModel
            {
                HospitalId = _hospitalId,
                FromDate = _today,
                ToDate = _today,
                PageNumber = 1,
                PageSize = 2,
            }, CancellationToken.None);

            Assert.That(page1.Bills, Has.Count.EqualTo(2), "only the page size, not every matching bill");
            Assert.That(page1.TotalBills, Is.EqualTo(4), "totals still reflect the full filtered set");
            Assert.That(page1.PageNumber, Is.EqualTo(1));
            Assert.That(page1.PageSize, Is.EqualTo(2));

            var page2 = await _handler.Handle(new GetPharmacyBillingHistoryRequestModel
            {
                HospitalId = _hospitalId,
                FromDate = _today,
                ToDate = _today,
                PageNumber = 2,
                PageSize = 2,
            }, CancellationToken.None);

            Assert.That(page2.Bills, Has.Count.EqualTo(2));
            Assert.That(page1.Bills.Select(b => b.InvoiceNo), Is.Not.EquivalentTo(page2.Bills.Select(b => b.InvoiceNo)), "pages must not overlap");
        }

        [Test]
        public async Task Handle_PageSizeOverCap_IsClampedTo200()
        {
            var response = await _handler.Handle(new GetPharmacyBillingHistoryRequestModel
            {
                HospitalId = _hospitalId,
                PageSize = 10000,
            }, CancellationToken.None);

            Assert.That(response.PageSize, Is.EqualTo(200));
        }

        [Test]
        public async Task Handle_NoMatchingBills_ReturnsEmpty()
        {
            var response = await _handler.Handle(new GetPharmacyBillingHistoryRequestModel
            {
                HospitalId = Guid.NewGuid(),
            }, CancellationToken.None);

            Assert.That(response.Bills, Is.Empty);
            Assert.That(response.TotalBills, Is.EqualTo(0));
            Assert.That(response.TotalAmount, Is.EqualTo(0));
        }
    }
}
