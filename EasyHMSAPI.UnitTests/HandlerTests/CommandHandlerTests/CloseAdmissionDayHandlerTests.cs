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
    public class CloseAdmissionDayHandlerTests
    {
        private AppDbContext _context = null!;
        private CloseAdmissionDayHandler _handler = null!;
        private Guid _hospitalId;
        private Guid _encounterId;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new CloseAdmissionDayHandler(_context);
            _hospitalId = Guid.NewGuid();
            _encounterId = Guid.NewGuid();
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private BillingChargeEvent SeedCharge(DateTime serviceDate, decimal net = 1000)
        {
            var charge = new BillingChargeEvent
            {
                ChargeEventId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EncounterId = _encounterId,
                DisplayName = "Room Charge",
                Qty = 1,
                UnitPrice = net,
                GrossAmount = net,
                DiscountAmount = 0,
                NetAmount = net,
                StatusCode = BillingConstants.ChargeEventStatus.Posted,
                ServiceDate = serviceDate,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            _context.BillingChargeEvent.Add(charge);
            _context.SaveChanges();
            return charge;
        }

        [Test]
        public async Task Handle_FirstClose_AnchorsToEarliestChargeServiceDate()
        {
            var anchor = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
            SeedCharge(anchor);

            var response = await _handler.Handle(new CloseAdmissionDayRequestModel
            {
                HospitalId = _hospitalId,
                EncounterId = _encounterId,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            var day1 = _context.AdmissionDayBill.Single(b => b.EncounterId == _encounterId);
            Assert.That(day1.DayNumber, Is.EqualTo(1));
            Assert.That(day1.FromUtc, Is.EqualTo(anchor));
            Assert.That(day1.ToUtc, Is.EqualTo(anchor.AddDays(1)));
        }

        [Test]
        public async Task Handle_SecondClose_KeepsAnchorPinnedToFirstClose_EvenIfANewerChargeHasAnEarlierServiceDate()
        {
            // Regression guard: CloseAdmissionDayHandler used to recompute
            // anchor = Min(ServiceDate) across ALL charges on every close, not just the first.
            // Once a charge can carry a backdated ServiceDate (this session's new feature), a
            // charge posted AFTER Day 1 closed but dated earlier than Day 1's own anchor would
            // shift every day's window boundaries retroactively -- including Day 1's, already
            // closed and printed. The anchor must stay pinned to Day 1's FromUtc forever after.
            var anchor = new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc);
            SeedCharge(anchor);

            var firstClose = await _handler.Handle(new CloseAdmissionDayRequestModel
            {
                HospitalId = _hospitalId,
                EncounterId = _encounterId,
            }, CancellationToken.None);
            Assert.That(firstClose.Success, Is.True);
            var day1 = _context.AdmissionDayBill.Single(b => b.EncounterId == _encounterId);

            // Simulate a charge landing in the DB with a ServiceDate earlier than the pinned
            // anchor (AddChargeEventHandler's own day-lock guard blocks this at the API layer --
            // this test isolates CloseAdmissionDayHandler's own resilience regardless of that).
            SeedCharge(anchor.AddDays(-5));

            var secondClose = await _handler.Handle(new CloseAdmissionDayRequestModel
            {
                HospitalId = _hospitalId,
                EncounterId = _encounterId,
            }, CancellationToken.None);

            Assert.That(secondClose.Success, Is.True);
            var day2 = _context.AdmissionDayBill.Single(b => b.EncounterId == _encounterId && b.DayNumber == 2);

            // Day 2 must pick up exactly where Day 1 left off -- not shift backwards.
            Assert.That(day2.FromUtc, Is.EqualTo(day1.ToUtc));
            Assert.That(day2.FromUtc, Is.EqualTo(anchor.AddDays(1)));

            // Day 1's own window must be untouched by the later close.
            var day1Reloaded = _context.AdmissionDayBill.Single(b => b.AdmissionDayBillId == day1.AdmissionDayBillId);
            Assert.That(day1Reloaded.FromUtc, Is.EqualTo(anchor));
            Assert.That(day1Reloaded.ToUtc, Is.EqualTo(anchor.AddDays(1)));
        }
    }
}
