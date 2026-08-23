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
            _handler = new AddChargeEventHandler(_context);
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
        public async Task Handle_PostsBackdatedCharge_WhenReasonProvided()
        {
            var pastDate = DateTime.UtcNow.AddDays(-3);
            var response = await _handler.Handle(new AddChargeEventRequestModel
            {
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EncounterId = _encounterId,
                ServiceDate = pastDate,
                BackdateReason = "Missed billing this visit",
                Charges = new List<ChargeDetail>
                {
                    new ChargeDetail { DisplayName = "Consultation", Qty = 1, Rate = 500, DiscountPercent = 0, CategoryCode = "CONSULT" },
                },
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            var detail = response.Data!.ChargeEvents!.Single();
            Assert.That(detail.IsBackdated, Is.True);
            Assert.That(detail.ServiceDate, Is.EqualTo(pastDate));

            var posted = _context.BillingChargeEvent.Single(c => c.EncounterId == _encounterId);
            Assert.That(posted.IsBackdated, Is.True);
            Assert.That(posted.BackdateReason, Is.EqualTo("Missed billing this visit"));
            Assert.That(posted.ServiceDate, Is.EqualTo(pastDate));
        }

        [Test]
        public async Task Handle_RejectsBackdatedCharge_WhenReasonMissing()
        {
            var response = await _handler.Handle(new AddChargeEventRequestModel
            {
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EncounterId = _encounterId,
                ServiceDate = DateTime.UtcNow.AddDays(-3),
                Charges = new List<ChargeDetail>
                {
                    new ChargeDetail { DisplayName = "Consultation", Qty = 1, Rate = 500, DiscountPercent = 0, CategoryCode = "CONSULT" },
                },
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("reason"));
            Assert.That(_context.BillingChargeEvent.Count(), Is.EqualTo(0));
        }

        [Test]
        public async Task Handle_RejectsCharge_WhenServiceDateInFuture()
        {
            var response = await _handler.Handle(new AddChargeEventRequestModel
            {
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EncounterId = _encounterId,
                ServiceDate = DateTime.UtcNow.AddDays(1),
                Charges = new List<ChargeDetail>
                {
                    new ChargeDetail { DisplayName = "Consultation", Qty = 1, Rate = 500, DiscountPercent = 0, CategoryCode = "CONSULT" },
                },
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("future"));
        }

        [Test]
        public async Task Handle_RejectsBackdatedCharge_WhenItFallsBeforeAnAlreadyClosedDay()
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
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new AddChargeEventRequestModel
            {
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EncounterId = _encounterId,
                ServiceDate = DateTime.UtcNow.AddDays(-5), // inside Day 1's already-closed window
                BackdateReason = "Late entry",
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
        public async Task Handle_AllowsBackdatedCharge_WhenDatedAfterTheLatestClosedDay()
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
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new AddChargeEventRequestModel
            {
                HospitalId = _hospitalId,
                PatientId = "PT001",
                EncounterId = _encounterId,
                ServiceDate = DateTime.UtcNow.AddDays(-2), // after Day 1's window closed
                BackdateReason = "Late entry",
                Charges = new List<ChargeDetail>
                {
                    new ChargeDetail { DisplayName = "Consultation", Qty = 1, Rate = 500, DiscountPercent = 0, CategoryCode = "CONSULT" },
                },
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
        }

        [Test]
        public async Task Handle_DefaultsServiceDateToNow_WhenNotSupplied()
        {
            // Regression guard: omitting ServiceDate must stay byte-identical to pre-backdating
            // behavior -- no reason required, IsBackdated false, dated "now."
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
            var detail = response.Data!.ChargeEvents!.Single();
            Assert.That(detail.IsBackdated, Is.False);
            Assert.That(detail.ServiceDate, Is.InRange(before, after));
        }
    }
}
