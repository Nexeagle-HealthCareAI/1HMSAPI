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
    // Covers order-level cancel: rejects when not found / already cancelled / any line already has
    // a report, and on success voids only THIS order's own LAB_PATH charges (matched by
    // SourceRefId), leaving unrelated charges on the same encounter untouched.
    [TestFixture]
    public class CancelPathologyOrderHandlerTests
    {
        private AppDbContext _context = null!;
        private CancelPathologyOrderHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new CancelPathologyOrderHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        private (Guid HospitalId, Guid OrderId) SeedOrder(string status = "PLACED")
        {
            var hospitalId = Guid.NewGuid();
            var orderId = Guid.NewGuid();
            _context.PathologyOrder.Add(new PathologyOrder
            {
                OrderId = orderId,
                HospitalId = hospitalId,
                PatientId = "PTID00000001",
                OrderNo = "ORD-1",
                Status = status,
            });
            _context.SaveChanges();
            return (hospitalId, orderId);
        }

        [Test]
        public async Task Handle_OrderNotFound_ReturnsFailure()
        {
            var response = await _handler.Handle(new CancelPathologyOrderCommand
            {
                HospitalId = Guid.NewGuid(),
                OrderId = Guid.NewGuid(),
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
        }

        [Test]
        public async Task Handle_AlreadyCancelled_ReturnsFailure()
        {
            var (hospitalId, orderId) = SeedOrder(status: "CANCELLED");

            var response = await _handler.Handle(new CancelPathologyOrderCommand
            {
                HospitalId = hospitalId,
                OrderId = orderId,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
        }

        [Test]
        public async Task Handle_LineAlreadyHasReport_ReturnsFailure()
        {
            var (hospitalId, orderId) = SeedOrder();
            _context.PathologyOrderLine.Add(new PathologyOrderLine
            {
                OrderLineId = Guid.NewGuid(),
                HospitalId = hospitalId,
                OrderId = orderId,
                TestId = Guid.NewGuid(),
                Status = "RESULT_ENTERED",
                ReportId = Guid.NewGuid(),
            });
            _context.SaveChanges();

            var response = await _handler.Handle(new CancelPathologyOrderCommand
            {
                HospitalId = hospitalId,
                OrderId = orderId,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(_context.PathologyOrder.Single(o => o.OrderId == orderId).Status, Is.Not.EqualTo("CANCELLED"));
        }

        [Test]
        public async Task Handle_NoReportYet_CancelsOrderAndVoidsOnlyItsOwnCharges()
        {
            var (hospitalId, orderId) = SeedOrder(status: "IN_PROGRESS");
            var encounterId = Guid.NewGuid();

            var ownCharge = new BillingChargeEvent
            {
                ChargeEventId = Guid.NewGuid(),
                HospitalId = hospitalId,
                PatientId = "PTID00000001",
                EncounterId = encounterId,
                SourceModule = BillingConstants.SourceModule.LabPath,
                SourceRefId = orderId.ToString(),
                DisplayName = "Complete Blood Count",
                Qty = 1,
                NetAmount = 250m,
                StatusCode = BillingConstants.ChargeEventStatus.Posted,
                ServiceDate = DateTime.UtcNow,
            };
            var unrelatedCharge = new BillingChargeEvent
            {
                ChargeEventId = Guid.NewGuid(),
                HospitalId = hospitalId,
                PatientId = "PTID00000001",
                EncounterId = encounterId,
                SourceModule = BillingConstants.SourceModule.LabPath,
                SourceRefId = Guid.NewGuid().ToString(),
                DisplayName = "Unrelated test on same encounter",
                Qty = 1,
                NetAmount = 300m,
                StatusCode = BillingConstants.ChargeEventStatus.Posted,
                ServiceDate = DateTime.UtcNow,
            };
            _context.BillingChargeEvent.AddRange(ownCharge, unrelatedCharge);
            _context.SaveChanges();

            var response = await _handler.Handle(new CancelPathologyOrderCommand
            {
                HospitalId = hospitalId,
                OrderId = orderId,
                LoggedInUserName = "tester",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            Assert.That(_context.PathologyOrder.Single(o => o.OrderId == orderId).Status, Is.EqualTo("CANCELLED"));

            var refreshedOwn = _context.BillingChargeEvent.Single(c => c.ChargeEventId == ownCharge.ChargeEventId);
            Assert.That(refreshedOwn.StatusCode, Is.EqualTo(BillingConstants.ChargeEventStatus.Void));
            Assert.That(refreshedOwn.VoidReason, Is.EqualTo("Pathology order cancelled"));

            var refreshedUnrelated = _context.BillingChargeEvent.Single(c => c.ChargeEventId == unrelatedCharge.ChargeEventId);
            Assert.That(refreshedUnrelated.StatusCode, Is.EqualTo(BillingConstants.ChargeEventStatus.Posted));
        }
    }
}
