using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.UnitTests.TestUtils;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class BulkAddExpenseHandlerTests
    {
        private AppDbContext _context = null!;
        private BulkAddExpenseHandler _handler = null!;
        private Guid _hospitalId;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new BulkAddExpenseHandler(_context);
            _hospitalId = Guid.NewGuid();
        }

        [TearDown]
        public void TearDown()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        [Test]
        public async Task Handle_MissingCategoryOrHospitalId_ReturnsError()
        {
            var response = await _handler.Handle(new BulkAddExpenseRequestModel { HospitalId = _hospitalId }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("required"));
        }

        [Test]
        public async Task Handle_NoLines_ReturnsError()
        {
            var response = await _handler.Handle(new BulkAddExpenseRequestModel { HospitalId = _hospitalId, CategoryCode = "FOOD" }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("one expense line"));
        }

        [Test]
        public async Task Handle_LineWithZeroAmount_ReturnsError()
        {
            var request = new BulkAddExpenseRequestModel
            {
                HospitalId = _hospitalId,
                CategoryCode = "FOOD",
                Lines = { new BulkExpenseLine { Amount = 0, Reason = "Tea" } },
            };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("greater than zero"));
        }

        [Test]
        public async Task Handle_MultipleLines_CreatesOneExpensePerLine_WithSharedFieldsAndOwnReason()
        {
            var request = new BulkAddExpenseRequestModel
            {
                HospitalId = _hospitalId,
                CategoryCode = "food",
                Vendor = "Canteen",
                PaymentMode = "cash",
                Lines =
                {
                    new BulkExpenseLine { Amount = 200, Reason = "Lunch for staff" },
                    new BulkExpenseLine { Amount = 150, Reason = "Tea and coffee" },
                    new BulkExpenseLine { Amount = 300, Reason = null },
                },
                LoggedInUserName = "cashier1",
            };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.CreatedCount, Is.EqualTo(3));
            Assert.That(response.ExpenseIds, Has.Count.EqualTo(3));

            var created = _context.Expenses.Where(e => e.HospitalId == _hospitalId).OrderBy(e => e.Amount).ToList();
            Assert.That(created, Has.Count.EqualTo(3));
            Assert.That(created.All(e => e.CategoryCode == "food"), Is.True);
            Assert.That(created.All(e => e.Vendor == "Canteen"), Is.True);
            Assert.That(created.All(e => e.PaymentMode == "CASH"), Is.True);
            Assert.That(created.All(e => e.CreatedBy == "cashier1"), Is.True);
            Assert.That(created.Any(e => e.Notes == "Lunch for staff"), Is.True);
            Assert.That(created.Any(e => e.Notes == "Tea and coffee"), Is.True);
            Assert.That(created.Any(e => e.Notes == null), Is.True);
        }
    }
}
