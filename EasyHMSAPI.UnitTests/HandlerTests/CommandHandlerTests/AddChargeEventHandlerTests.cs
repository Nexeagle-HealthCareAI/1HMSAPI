using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class AddChargeEventHandlerTests
    {
        private AppDbContext _context = null!;
        private AddChargeEventHandler _handler = null!;
        private Guid _hospitalId;
        private Guid _encounterId;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new AddChargeEventHandler(_context, NullLogger<AddChargeEventHandler>.Instance);
            _hospitalId = Guid.NewGuid();
            _encounterId = Guid.NewGuid();

            _context.Encounter.Add(new Encounter
            {
                EncounterId = _encounterId,
                HospitalId = _hospitalId,
                PatientId = "PT001",
                StatusCode = BillingConstants.EncounterStatus.Open,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            _context.SaveChanges();
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        [Test]
        public async Task Handle_DiscountExceedsChargeMasterCap_PostsWithoutApproval()
        {
            var chargeMaster = new ChargeMaster
            {
                ChargeId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                ChargeCode = "PROC1",
                DisplayName = "Minor Procedure",
                CategoryCode = "PROCEDURE",
                DefaultRate = 1000,
                DefaultQty = 1,
                MaxDiscountPercent = 10, // cap
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            _context.ChargeMaster.Add(chargeMaster);
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new AddChargeEventRequestModel
            {
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EncounterId = _encounterId,
                Charges = new List<ChargeDetail>
                {
                    new ChargeDetail
                    {
                        ChargeId = chargeMaster.ChargeId,
                        DisplayName = "Minor Procedure",
                        Qty = 1,
                        Rate = 1000,
                        DiscountPercent = 50, // well beyond the 10% cap
                        CategoryCode = "PROCEDURE",
                    },
                },
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            var detail = response.Data!.ChargeEvents!.Single();
            Assert.That(detail.DiscountApprovalRequired, Is.False);
            Assert.That(detail.DiscountApprovalId, Is.Null);
            Assert.That(detail.NetAmount, Is.EqualTo(500));

            Assert.That(_context.DiscountApproval.Count(), Is.EqualTo(0));

            var posted = _context.BillingChargeEvent.Single(c => c.EncounterId == _encounterId);
            Assert.That(posted.DiscountAmount, Is.EqualTo(500));
            Assert.That(posted.StatusCode, Is.EqualTo(BillingConstants.ChargeEventStatus.Posted));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public async Task Handle_RejectsCharge_WhenQtyIsZeroOrNegative(decimal qty)
        {
            // Regression guard: this endpoint is reachable directly (not just via the web UI's own
            // validation), and previously a zero/negative Qty posted straight through into
            // GrossAmount/NetAmount with no server-side check -- same guardrail
            // UpdateChargeEventHandler already enforces on a single-line edit.
            var response = await _handler.Handle(new AddChargeEventRequestModel
            {
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EncounterId = _encounterId,
                Charges = new List<ChargeDetail>
                {
                    new ChargeDetail { DisplayName = "Bad Line", Qty = qty, Rate = 100, DiscountPercent = 0, CategoryCode = "PROCEDURE" },
                },
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("Quantity"));
            Assert.That(_context.BillingChargeEvent.Count(), Is.EqualTo(0));
        }

        [Test]
        public async Task Handle_RejectsCharge_WhenRateIsNegative()
        {
            var response = await _handler.Handle(new AddChargeEventRequestModel
            {
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EncounterId = _encounterId,
                Charges = new List<ChargeDetail>
                {
                    new ChargeDetail { DisplayName = "Bad Line", Qty = 1, Rate = -50, DiscountPercent = 0, CategoryCode = "PROCEDURE" },
                },
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("Rate"));
            Assert.That(_context.BillingChargeEvent.Count(), Is.EqualTo(0));
        }

        [Test]
        public async Task Handle_RejectsCharge_WhenDiscountPercentIsNegative()
        {
            var response = await _handler.Handle(new AddChargeEventRequestModel
            {
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EncounterId = _encounterId,
                Charges = new List<ChargeDetail>
                {
                    new ChargeDetail { DisplayName = "Bad Line", Qty = 1, Rate = 100, DiscountPercent = -10, CategoryCode = "PROCEDURE" },
                },
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("Discount"));
            Assert.That(_context.BillingChargeEvent.Count(), Is.EqualTo(0));
        }

        [Test]
        public async Task Handle_CapsDiscount_WhenDiscountPercentExceeds100()
        {
            // Without a cap, a >100% discount drives NetAmount negative -- a charge that pays the
            // patient. Same cap-at-gross UpdateChargeEventHandler already applies.
            var response = await _handler.Handle(new AddChargeEventRequestModel
            {
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EncounterId = _encounterId,
                Charges = new List<ChargeDetail>
                {
                    new ChargeDetail { DisplayName = "Line", Qty = 1, Rate = 1000, DiscountPercent = 150, CategoryCode = "PROCEDURE" },
                },
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            var detail = response.Data!.ChargeEvents!.Single();
            Assert.That(detail.DiscountAmount, Is.EqualTo(1000));
            Assert.That(detail.NetAmount, Is.EqualTo(0));
        }

        [Test]
        public async Task Handle_UsesEncounterServiceDate_ForNewCharges()
        {
            // The date now lives once on the visit (Encounter.ServiceDate), not per Add-Charge call.
            var encounter = _context.Encounter.Single(e => e.EncounterId == _encounterId);
            var visitDate = DateTime.UtcNow.AddDays(-3).Date;
            encounter.ServiceDate = visitDate;
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new AddChargeEventRequestModel
            {
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EncounterId = _encounterId,
                Charges = new List<ChargeDetail>
                {
                    new ChargeDetail { DisplayName = "Consultation", Qty = 1, Rate = 500, DiscountPercent = 0, CategoryCode = "CONSULT" },
                },
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            var posted = _context.BillingChargeEvent.Single(c => c.EncounterId == _encounterId);
            Assert.That(posted.ServiceDate.Date, Is.EqualTo(visitDate), "The charge must land on the visit's chosen date.");
        }

        [Test]
        public async Task Handle_DefaultsServiceDateToNow_WhenEncounterServiceDateNotSet()
        {
            // Regression guard: a visit with no ServiceDate override must stay byte-identical to
            // pre-feature behavior -- charges post at real "now".
            var before = DateTime.UtcNow;
            var response = await _handler.Handle(new AddChargeEventRequestModel
            {
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EncounterId = _encounterId,
                Charges = new List<ChargeDetail>
                {
                    new ChargeDetail { DisplayName = "Consultation", Qty = 1, Rate = 500, DiscountPercent = 0, CategoryCode = "CONSULT" },
                },
            }, CancellationToken.None);
            var after = DateTime.UtcNow;

            Assert.That(response.Success, Is.True);
            var posted = _context.BillingChargeEvent.Single(c => c.EncounterId == _encounterId);
            Assert.That(posted.ServiceDate, Is.InRange(before, after));
        }

        [Test]
        public async Task Handle_RejectsCharge_WhenVisitDateFallsBeforeAnAlreadyClosedDay()
        {
            var dayBill = new AdmissionDayBill
            {
                AdmissionDayBillId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                EncounterId = _encounterId,
                PatientId = "PT001",
                DayNumber = 1,
                FromUtc = DateTime.UtcNow.AddDays(-5),
                ToUtc = DateTime.UtcNow.AddDays(-4),
                InterimBillNo = "IB-1",
                StatusCode = BillingConstants.DayBillStatus.Closed,
                ClosedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            _context.AdmissionDayBill.Add(dayBill);
            var encounter = _context.Encounter.Single(e => e.EncounterId == _encounterId);
            encounter.ServiceDate = DateTime.UtcNow.AddDays(-5); // inside Day 1's already-closed window
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new AddChargeEventRequestModel
            {
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EncounterId = _encounterId,
                Charges = new List<ChargeDetail>
                {
                    new ChargeDetail { DisplayName = "Consultation", Qty = 1, Rate = 500, DiscountPercent = 0, CategoryCode = "CONSULT" },
                },
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("Day 1"));
            Assert.That(response.Message, Does.Contain("IB-1"));
            Assert.That(_context.BillingChargeEvent.Count(), Is.EqualTo(0));
        }

        [Test]
        public async Task Handle_AllowsCharge_WhenVisitDateIsAfterTheLatestClosedDay()
        {
            var dayBill = new AdmissionDayBill
            {
                AdmissionDayBillId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                EncounterId = _encounterId,
                PatientId = "PT001",
                DayNumber = 1,
                FromUtc = DateTime.UtcNow.AddDays(-5),
                ToUtc = DateTime.UtcNow.AddDays(-4),
                InterimBillNo = "IB-1",
                StatusCode = BillingConstants.DayBillStatus.Closed,
                ClosedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            _context.AdmissionDayBill.Add(dayBill);
            var encounter = _context.Encounter.Single(e => e.EncounterId == _encounterId);
            encounter.ServiceDate = DateTime.UtcNow.AddDays(-2); // after Day 1's window closed
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new AddChargeEventRequestModel
            {
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EncounterId = _encounterId,
                Charges = new List<ChargeDetail>
                {
                    new ChargeDetail { DisplayName = "Consultation", Qty = 1, Rate = 500, DiscountPercent = 0, CategoryCode = "CONSULT" },
                },
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
        }
    }
}
