using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class PharmacyAnalyticsQueryHandlersTests
    {
        private AppDbContext _context = null!;
        private PharmacyAnalyticsQueryHandlers _handler = null!;
        private Guid _hospitalId;
        private Guid _itemId1;
        private Guid _itemId2;
        private Guid _chargeId1;
        private Guid _chargeId2;
        private DateTime _today;

        [SetUp]
        public async Task SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new PharmacyAnalyticsQueryHandlers(_context);

            _hospitalId = Guid.NewGuid();
            _itemId1 = Guid.NewGuid();
            _itemId2 = Guid.NewGuid();
            _chargeId1 = Guid.NewGuid();
            _chargeId2 = Guid.NewGuid();
            _today = DateTime.UtcNow.Date;

            _context.InventoryItem.Add(new InventoryItem
            {
                InventoryItemId = _itemId1, HospitalId = _hospitalId, ItemCode = "PARA", ItemName = "Paracetamol",
                Category = "DRUG", Unit = "TAB", ChargeId = _chargeId1, CurrentStock = 0, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            _context.InventoryItem.Add(new InventoryItem
            {
                InventoryItemId = _itemId2, HospitalId = _hospitalId, ItemCode = "AMOX", ItemName = "Amoxicillin",
                Category = "DRUG", Unit = "TAB", ChargeId = _chargeId2, CurrentStock = 0, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });

            // High-value charge (in range) — pharmacy counter sale.
            _context.BillingChargeEvent.Add(new BillingChargeEvent
            {
                ChargeEventId = Guid.NewGuid(), HospitalId = _hospitalId, EncounterId = Guid.NewGuid(),
                ChargeId = _chargeId1, DisplayName = "Paracetamol", SourceModule = "PHARMACY_COUNTER",
                Qty = 7, UnitPrice = 100, NetAmount = 700, StatusCode = "POSTED",
                HsnSacCode = "3004", GstRate = 12, TaxableAmount = 1000, CgstAmount = 60, SgstAmount = 60, TaxAmount = 120,
                ServiceDate = _today, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            // Lower-value charge (in range) — pharmacy IPD sale.
            _context.BillingChargeEvent.Add(new BillingChargeEvent
            {
                ChargeEventId = Guid.NewGuid(), HospitalId = _hospitalId, EncounterId = Guid.NewGuid(),
                ChargeId = _chargeId2, DisplayName = "Amoxicillin", SourceModule = "PHARMACY_IPD",
                Qty = 6, UnitPrice = 50, NetAmount = 300, StatusCode = "POSTED",
                HsnSacCode = "3004", GstRate = 5, TaxableAmount = 100, CgstAmount = 2.5m, SgstAmount = 2.5m, TaxAmount = 5,
                ServiceDate = _today, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            // Voided charge — must be excluded everywhere.
            _context.BillingChargeEvent.Add(new BillingChargeEvent
            {
                ChargeEventId = Guid.NewGuid(), HospitalId = _hospitalId, EncounterId = Guid.NewGuid(),
                ChargeId = _chargeId1, DisplayName = "Paracetamol", SourceModule = "PHARMACY_COUNTER",
                Qty = 5, UnitPrice = 100, NetAmount = 500, StatusCode = "VOID",
                ServiceDate = _today, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            // Non-pharmacy charge — must be excluded everywhere.
            _context.BillingChargeEvent.Add(new BillingChargeEvent
            {
                ChargeEventId = Guid.NewGuid(), HospitalId = _hospitalId, EncounterId = Guid.NewGuid(),
                DisplayName = "Consultation", SourceModule = "OPD",
                Qty = 1, UnitPrice = 500, NetAmount = 500, StatusCode = "POSTED",
                ServiceDate = _today, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });

            await _context.SaveChangesAsync();
        }

        [TearDown]
        public void TearDown()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        [Test]
        public async Task SalesTrend_GroupsByDayAndExcludesVoidAndNonPharmacy()
        {
            var response = await _handler.Handle(new GetPharmacySalesTrendRequestModel
            {
                HospitalId = _hospitalId, FromDate = _today.AddDays(-1), ToDate = _today.AddDays(1), GroupBy = "DAY",
            }, CancellationToken.None);

            Assert.That(response.Points, Has.Count.EqualTo(1));
            Assert.That(response.Points[0].TotalSales, Is.EqualTo(1000));
            Assert.That(response.Points[0].TotalQty, Is.EqualTo(13));
            Assert.That(response.Points[0].LineCount, Is.EqualTo(2));
        }

        [Test]
        public async Task AbcAnalysis_RanksByValueAndClassifiesAB()
        {
            var response = await _handler.Handle(new GetPharmacyAbcAnalysisRequestModel
            {
                HospitalId = _hospitalId, FromDate = _today.AddDays(-1), ToDate = _today.AddDays(1),
            }, CancellationToken.None);

            Assert.That(response.Items, Has.Count.EqualTo(2));
            Assert.That(response.Items[0].ItemName, Is.EqualTo("Paracetamol"));
            Assert.That(response.Items[0].CumulativePercent, Is.EqualTo(70m));
            Assert.That(response.Items[0].Class, Is.EqualTo("A"));
            Assert.That(response.Items[1].ItemName, Is.EqualTo("Amoxicillin"));
            Assert.That(response.Items[1].CumulativePercent, Is.EqualTo(100m));
            Assert.That(response.Items[1].Class, Is.EqualTo("C"));
        }

        [Test]
        public async Task GstLiability_GroupsByHsnAndRate()
        {
            var response = await _handler.Handle(new GetPharmacyGstLiabilityRequestModel
            {
                HospitalId = _hospitalId, FromDate = _today.AddDays(-1), ToDate = _today.AddDays(1),
            }, CancellationToken.None);

            Assert.That(response.Rows, Has.Count.EqualTo(2));
            Assert.That(response.GrandTotalTax, Is.EqualTo(125));
            var twelvePercentRow = response.Rows.Single(r => r.GstRate == 12);
            Assert.That(twelvePercentRow.TotalTax, Is.EqualTo(120));
            Assert.That(twelvePercentRow.TaxableAmount, Is.EqualTo(1000));
        }

        [Test]
        public async Task ExpiryLossPrevented_SumsRecoveredAndAtRiskValues()
        {
            var vendorId = Guid.NewGuid();
            _context.Vendor.Add(new Vendor
            {
                VendorId = vendorId, HospitalId = _hospitalId, VendorCode = "V1", VendorName = "Acme",
                IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            _context.VendorReturnNote.Add(new VendorReturnNote
            {
                VendorReturnId = Guid.NewGuid(), HospitalId = _hospitalId, VendorId = vendorId,
                ReturnNoteNo = "RTV-1", TotalQty = 5, TotalValue = 250, GeneratedAt = _today, CreatedAt = DateTime.UtcNow,
            });
            // Red-bucket batch (< 30 days to expiry) — at risk.
            _context.Batch.Add(new Batch
            {
                BatchId = Guid.NewGuid(), HospitalId = _hospitalId, InventoryItemId = _itemId1, StoreId = Guid.NewGuid(),
                BatchNumber = "RED", ExpiryDate = _today.AddDays(10), RemainingQty = 4, UnitCost = 20,
                Status = "ACTIVE", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            // Green-bucket batch — not at risk.
            _context.Batch.Add(new Batch
            {
                BatchId = Guid.NewGuid(), HospitalId = _hospitalId, InventoryItemId = _itemId1, StoreId = Guid.NewGuid(),
                BatchNumber = "GREEN", ExpiryDate = _today.AddDays(300), RemainingQty = 4, UnitCost = 20,
                Status = "ACTIVE", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetPharmacyExpiryLossPreventedRequestModel
            {
                HospitalId = _hospitalId, FromDate = _today.AddDays(-1), ToDate = _today.AddDays(1),
            }, CancellationToken.None);

            Assert.That(response.RecoveredValue, Is.EqualTo(250));
            Assert.That(response.RtvNoteCount, Is.EqualTo(1));
            Assert.That(response.AtRiskValue, Is.EqualTo(80));
            Assert.That(response.AtRiskBatchCount, Is.EqualTo(1));
        }
    }
}
