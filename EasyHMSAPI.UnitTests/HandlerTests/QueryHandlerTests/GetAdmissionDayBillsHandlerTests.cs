using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class GetAdmissionDayBillsHandlerTests
    {
        private AppDbContext _context = null!;
        private GetAdmissionDayBillsHandler _handler = null!;
        private CloseAdmissionDayHandler _closeHandler = null!;
        private Guid _hospitalId;
        private Guid _encounterId;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetAdmissionDayBillsHandler(_context);
            _closeHandler = new CloseAdmissionDayHandler(_context);
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
        public async Task Handle_BackdatedVisit_NoClosedDays_TotalDays_IsNotInflatedByElapsedRealTime()
        {
            // Regression guard: totalDays used to factor in DayIndexOf(now, anchor) -- "how many
            // real calendar days have passed since the visit's date" -- rather than "how many days
            // actually have billing content". A visit backdated 10 days ago with just one charge
            // must show exactly 1 day, not 11 empty rows stretching to today.
            var backdate = DateTime.UtcNow.Date.AddDays(-10);
            SeedCharge(backdate);

            var response = await _handler.Handle(new GetAdmissionDayBillsRequestModel
            {
                HospitalId = _hospitalId,
                EncounterId = _encounterId,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.Data!.TotalDays, Is.EqualTo(1));
            Assert.That(response.Data.Days, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task Handle_AfterClosingOneDay_OnABackdatedVisit_TotalDays_StaysAtOne()
        {
            var backdate = DateTime.UtcNow.Date.AddDays(-10);
            SeedCharge(backdate);

            var closeResponse = await _closeHandler.Handle(new CloseAdmissionDayRequestModel
            {
                HospitalId = _hospitalId,
                EncounterId = _encounterId,
            }, CancellationToken.None);
            Assert.That(closeResponse.Success, Is.True);

            var response = await _handler.Handle(new GetAdmissionDayBillsRequestModel
            {
                HospitalId = _hospitalId,
                EncounterId = _encounterId,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.Data!.TotalDays, Is.EqualTo(1),
                "With Day 1 closed and no further live charges, no empty days up to today should be synthesized.");
            Assert.That(response.Data.Days.Single().IsClosed, Is.True);
        }

        [Test]
        public async Task Handle_AnchorStaysPinnedToFirstClose_EvenIfALaterChargeHasAnEarlierServiceDate()
        {
            // Same invariant CloseAdmissionDayHandlerTests enforces for the write side: once Day 1
            // has closed, the read side must not re-derive anchor = Min(ServiceDate) either, or a
            // later charge dated earlier than the pinned anchor would shift Day 1's own window.
            var anchor = DateTime.UtcNow.Date.AddDays(-10);
            SeedCharge(anchor);
            var closeResponse = await _closeHandler.Handle(new CloseAdmissionDayRequestModel
            {
                HospitalId = _hospitalId,
                EncounterId = _encounterId,
            }, CancellationToken.None);
            Assert.That(closeResponse.Success, Is.True);

            SeedCharge(anchor.AddDays(-5)); // lands in the DB with an earlier ServiceDate than the pinned anchor

            var response = await _handler.Handle(new GetAdmissionDayBillsRequestModel
            {
                HospitalId = _hospitalId,
                EncounterId = _encounterId,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            var day1 = response.Data!.Days.Single(d => d.DayNumber == 1);
            Assert.That(day1.FromUtc, Is.EqualTo(anchor), "Day 1's window must not shift once it's closed.");
            Assert.That(response.Data.AdmittedAt, Is.EqualTo(anchor));
        }

        [Test]
        public async Task Handle_LiveChargeAfterClosedDay_AppearsOnAFreshOpenDay_NotInflatingPastIt()
        {
            var anchor = DateTime.UtcNow.Date.AddDays(-10);
            SeedCharge(anchor);
            await _closeHandler.Handle(new CloseAdmissionDayRequestModel
            {
                HospitalId = _hospitalId,
                EncounterId = _encounterId,
            }, CancellationToken.None);

            // A charge posted for the second day of the (backdated) stay -- still content, so it's
            // legitimate for totalDays to grow to 2, but no further than that.
            SeedCharge(anchor.AddDays(1).AddHours(2));

            var response = await _handler.Handle(new GetAdmissionDayBillsRequestModel
            {
                HospitalId = _hospitalId,
                EncounterId = _encounterId,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.Data!.TotalDays, Is.EqualTo(2));
            var day2 = response.Data.Days.Single(d => d.DayNumber == 2);
            Assert.That(day2.IsClosed, Is.False);
            Assert.That(day2.NetAmount, Is.EqualTo(1000));
        }
    }
}
