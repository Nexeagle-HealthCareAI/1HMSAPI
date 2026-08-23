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
    public class GetBillingCategoryAnalyticsHandlerTests
    {
        private AppDbContext _context = null!;
        private GetBillingCategoryAnalyticsHandler _handler = null!;
        private Guid _hospitalId;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetBillingCategoryAnalyticsHandler(_context);
            _hospitalId = Guid.NewGuid();
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private void SeedCharge(DateTime serviceDate, string categoryCode, decimal netAmount)
        {
            _context.BillingChargeEvent.Add(new BillingChargeEvent
            {
                ChargeEventId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EncounterId = Guid.NewGuid(),
                DisplayName = categoryCode,
                CategoryCode = categoryCode,
                Qty = 1,
                UnitPrice = netAmount,
                NetAmount = netAmount,
                StatusCode = BillingConstants.ChargeEventStatus.Posted,
                ServiceDate = serviceDate,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
        }

        private void SeedExpense(DateTime expenseDate, string categoryCode, decimal amount)
        {
            _context.Expenses.Add(new Expense
            {
                ExpenseId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                ExpenseDate = expenseDate,
                CategoryCode = categoryCode,
                Amount = amount,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
        }

        [Test]
        public async Task Handle_AllTime_IncludesEverythingRegardlessOfDate()
        {
            SeedCharge(DateTime.UtcNow.AddDays(-100), "LAB", 500);
            SeedCharge(DateTime.UtcNow, "PHARMACY", 300);
            SeedExpense(DateTime.UtcNow.AddDays(-50), "RENT", 1000);
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetBillingCategoryAnalyticsRequestModel { HospitalId = _hospitalId }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.Data!.TotalRevenue, Is.EqualTo(800));
            Assert.That(response.Data.TotalExpense, Is.EqualTo(1000));
            Assert.That(response.Data.NetAmount, Is.EqualTo(-200));
            Assert.That(response.Data.RevenueByCategory, Has.Count.EqualTo(2));
        }

        [Test]
        public async Task Handle_SingleDay_OnlyIncludesThatDay()
        {
            var targetDay = new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc);
            SeedCharge(targetDay, "LAB", 500);
            SeedCharge(targetDay.AddDays(-1), "LAB", 999); // day before -- must be excluded
            SeedCharge(targetDay.AddDays(1), "LAB", 999);  // day after -- must be excluded
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetBillingCategoryAnalyticsRequestModel
            {
                HospitalId = _hospitalId,
                StartDate = targetDay.Date,
                EndDate = targetDay.Date,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.Data!.TotalRevenue, Is.EqualTo(500));
        }

        [Test]
        public async Task Handle_DateRange_IsInclusiveOfBothEndpoints()
        {
            var start = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
            var end = new DateTime(2026, 6, 5, 0, 0, 0, DateTimeKind.Utc);
            SeedCharge(start, "OT", 100);                     // first day, midnight -- must be included
            SeedCharge(end.AddHours(23), "OT", 200);           // last day, near end of day -- must be included
            SeedCharge(start.AddDays(-1), "OT", 999);          // before range -- excluded
            SeedCharge(end.AddDays(1), "OT", 999);             // after range -- excluded
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetBillingCategoryAnalyticsRequestModel
            {
                HospitalId = _hospitalId,
                StartDate = start,
                EndDate = end,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.Data!.TotalRevenue, Is.EqualTo(300));
        }

        [Test]
        public async Task Handle_GroupsRevenueByCategoryCode_NotByEncounter()
        {
            SeedCharge(DateTime.UtcNow, "PHARMACY", 100);
            SeedCharge(DateTime.UtcNow, "PHARMACY", 150);
            SeedCharge(DateTime.UtcNow, "LAB", 400);
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetBillingCategoryAnalyticsRequestModel { HospitalId = _hospitalId }, CancellationToken.None);

            var pharmacy = response.Data!.RevenueByCategory.Single(c => c.CategoryCode == "PHARMACY");
            Assert.That(pharmacy.Amount, Is.EqualTo(250));
            Assert.That(pharmacy.Count, Is.EqualTo(2));

            var lab = response.Data.RevenueByCategory.Single(c => c.CategoryCode == "LAB");
            Assert.That(lab.Amount, Is.EqualTo(400));
        }

        [Test]
        public async Task Handle_ExcludesVoidedCharges()
        {
            SeedCharge(DateTime.UtcNow, "LAB", 500);
            var voided = new BillingChargeEvent
            {
                ChargeEventId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EncounterId = Guid.NewGuid(),
                CategoryCode = "LAB",
                Qty = 1,
                UnitPrice = 999,
                NetAmount = 999,
                StatusCode = BillingConstants.ChargeEventStatus.Void,
                ServiceDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            _context.BillingChargeEvent.Add(voided);
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetBillingCategoryAnalyticsRequestModel { HospitalId = _hospitalId }, CancellationToken.None);

            Assert.That(response.Data!.TotalRevenue, Is.EqualTo(500));
        }

        [Test]
        public async Task Handle_ScopesToTheRequestedHospitalOnly()
        {
            SeedCharge(DateTime.UtcNow, "LAB", 500);
            var otherHospitalCharge = new BillingChargeEvent
            {
                ChargeEventId = Guid.NewGuid(),
                HospitalId = Guid.NewGuid(),
                PatientId = "PT002",
                EncounterId = Guid.NewGuid(),
                CategoryCode = "LAB",
                Qty = 1,
                UnitPrice = 999,
                NetAmount = 999,
                StatusCode = BillingConstants.ChargeEventStatus.Posted,
                ServiceDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            _context.BillingChargeEvent.Add(otherHospitalCharge);
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetBillingCategoryAnalyticsRequestModel { HospitalId = _hospitalId }, CancellationToken.None);

            Assert.That(response.Data!.TotalRevenue, Is.EqualTo(500));
        }
    }
}
