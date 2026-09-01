using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using MediatR;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    // Covers per-line report generation: each PathologyOrderLine (test) gets its own independent
    // report, generatable as soon as that one line has a result -- a sibling line in the same
    // order lacking a result no longer blocks it. There is no separate technician-sign /
    // pathologist-approve step (both handlers were removed) -- this is the single, freely
    // re-callable "generate/update report" action per line. Regenerating a line that already has a
    // report must reuse it (same ReportId/ReportNo) rather than reject, and must NOT re-dispatch the
    // ON_REPORT_APPROVAL billing trigger a second time for that same line -- AddChargeEventHandler
    // has no dedup for this caller, so firing it again on every "Update Report" click would
    // double-bill the same test.
    [TestFixture]
    public class GeneratePathologyReportHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<IMediator> _mediatorMock = null!;
        private GeneratePathologyReportHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _mediatorMock = new Mock<IMediator>();
            _mediatorMock
                .Setup(m => m.Send(It.IsAny<AddChargeEventRequestModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AddChargeEventResponseModel { Success = true });
            _handler = new GeneratePathologyReportHandler(_context, _mediatorMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        private Guid SeedOrder(Guid hospitalId, string orderStatus = "IN_PROGRESS", Guid? encounterId = null)
        {
            var orderId = Guid.NewGuid();
            _context.PathologyOrder.Add(new PathologyOrder
            {
                OrderId = orderId,
                HospitalId = hospitalId,
                PatientId = "PTID00000001",
                OrderNo = "ORD-1",
                Status = orderStatus,
                EncounterId = encounterId,
            });
            return orderId;
        }

        private Guid SeedLineWithResult(Guid hospitalId, Guid orderId, Guid? testIdOverride = null, string lineStatus = "RESULT_ENTERED")
        {
            var lineId = Guid.NewGuid();
            _context.PathologyOrderLine.Add(new PathologyOrderLine
            {
                OrderLineId = lineId,
                HospitalId = hospitalId,
                OrderId = orderId,
                TestId = testIdOverride ?? Guid.NewGuid(),
                Status = lineStatus,
            });
            _context.PathologyResult.Add(new PathologyResult
            {
                ResultId = Guid.NewGuid(),
                HospitalId = hospitalId,
                OrderLineId = lineId,
                ResultValuesJson = "{\"Hemoglobin\":{\"value\":\"13.5\",\"flag\":\"NORMAL\"}}",
            });
            return lineId;
        }

        private Guid SeedLineWithoutResult(Guid hospitalId, Guid orderId)
        {
            var lineId = Guid.NewGuid();
            _context.PathologyOrderLine.Add(new PathologyOrderLine
            {
                OrderLineId = lineId,
                HospitalId = hospitalId,
                OrderId = orderId,
                TestId = Guid.NewGuid(),
                Status = "PENDING",
            });
            return lineId;
        }

        /// Single-line order, seeded with a result -- the common case. Returns (hospitalId, orderId, lineId).
        private (Guid HospitalId, Guid OrderId, Guid LineId) SeedSingleLineOrderWithResult(
            Guid? testIdOverride = null, Guid? encounterId = null)
        {
            var hospitalId = Guid.NewGuid();
            var orderId = SeedOrder(hospitalId, encounterId: encounterId);
            var lineId = SeedLineWithResult(hospitalId, orderId, testIdOverride);
            _context.SaveChanges();
            return (hospitalId, orderId, lineId);
        }

        [Test]
        public async Task Handle_OrderNotFound_ReturnsFailure()
        {
            var response = await _handler.Handle(new GeneratePathologyReportCommand
            {
                HospitalId = Guid.NewGuid(),
                OrderId = Guid.NewGuid(),
                OrderLineId = Guid.NewGuid(),
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
        }

        [Test]
        public async Task Handle_LineNotFound_ReturnsFailure()
        {
            var hospitalId = Guid.NewGuid();
            var orderId = SeedOrder(hospitalId);
            _context.SaveChanges();

            var response = await _handler.Handle(new GeneratePathologyReportCommand
            {
                HospitalId = hospitalId,
                OrderId = orderId,
                OrderLineId = Guid.NewGuid(),
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
        }

        [Test]
        public async Task Handle_ResultNotEnteredForLine_ReturnsFailure()
        {
            var hospitalId = Guid.NewGuid();
            var orderId = SeedOrder(hospitalId);
            var lineId = SeedLineWithoutResult(hospitalId, orderId);
            _context.SaveChanges();

            var response = await _handler.Handle(new GeneratePathologyReportCommand
            {
                HospitalId = hospitalId,
                OrderId = orderId,
                OrderLineId = lineId,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(_context.PathologyReport.Any(), Is.False);
        }

        [Test]
        public async Task Handle_GeneratingOneLine_SucceedsEvenWhileSiblingLineHasNoResult()
        {
            var hospitalId = Guid.NewGuid();
            var orderId = SeedOrder(hospitalId);
            var readyLineId = SeedLineWithResult(hospitalId, orderId);
            var pendingLineId = SeedLineWithoutResult(hospitalId, orderId);
            _context.SaveChanges();

            var response = await _handler.Handle(new GeneratePathologyReportCommand
            {
                HospitalId = hospitalId,
                OrderId = orderId,
                OrderLineId = readyLineId,
                LoggedInUserName = "tester",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);

            var readyLine = _context.PathologyOrderLine.Single(l => l.OrderLineId == readyLineId);
            Assert.That(readyLine.ReportId, Is.EqualTo(response.ReportId));

            var pendingLine = _context.PathologyOrderLine.Single(l => l.OrderLineId == pendingLineId);
            Assert.That(pendingLine.ReportId, Is.Null);
        }

        [Test]
        public async Task Handle_SingleLineOrder_GeneratingItsReportCompletesOrder()
        {
            var (hospitalId, orderId, lineId) = SeedSingleLineOrderWithResult();

            var response = await _handler.Handle(new GeneratePathologyReportCommand
            {
                HospitalId = hospitalId,
                OrderId = orderId,
                OrderLineId = lineId,
                LoggedInUserName = "tester",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            Assert.That(response.ReportId, Is.Not.Null);
            Assert.That(response.ReportNo, Is.Not.Null.And.Not.Empty);

            var order = _context.PathologyOrder.Single(o => o.OrderId == orderId);
            Assert.That(order.Status, Is.EqualTo("COMPLETED"));

            var line = _context.PathologyOrderLine.Single(l => l.OrderLineId == lineId);
            Assert.That(line.ReportId, Is.EqualTo(response.ReportId));

            var result = _context.PathologyResult.Single(r => r.OrderLineId == lineId);
            Assert.That(result.ReportId, Is.EqualTo(response.ReportId));

            var report = _context.PathologyReport.Single(r => r.ReportId == response.ReportId);
            Assert.That(report.Status, Is.EqualTo("GENERATED"));
            Assert.That(report.GeneratedAt, Is.Not.Null);
        }

        [Test]
        public async Task Handle_MultiLineOrder_OrderStaysIncompleteUntilAllLinesReported()
        {
            var hospitalId = Guid.NewGuid();
            var orderId = SeedOrder(hospitalId);
            var lineOne = SeedLineWithResult(hospitalId, orderId);
            var lineTwo = SeedLineWithResult(hospitalId, orderId);
            _context.SaveChanges();

            await _handler.Handle(new GeneratePathologyReportCommand
            {
                HospitalId = hospitalId,
                OrderId = orderId,
                OrderLineId = lineOne,
                LoggedInUserName = "tester",
            }, CancellationToken.None);

            Assert.That(_context.PathologyOrder.Single(o => o.OrderId == orderId).Status, Is.Not.EqualTo("COMPLETED"));

            await _handler.Handle(new GeneratePathologyReportCommand
            {
                HospitalId = hospitalId,
                OrderId = orderId,
                OrderLineId = lineTwo,
                LoggedInUserName = "tester",
            }, CancellationToken.None);

            Assert.That(_context.PathologyOrder.Single(o => o.OrderId == orderId).Status, Is.EqualTo("COMPLETED"));
        }

        [Test]
        public async Task Handle_CalledAgainForSameLine_ReusesExistingReport()
        {
            var (hospitalId, orderId, lineId) = SeedSingleLineOrderWithResult();

            var first = await _handler.Handle(new GeneratePathologyReportCommand
            {
                HospitalId = hospitalId,
                OrderId = orderId,
                OrderLineId = lineId,
                LoggedInUserName = "tester",
            }, CancellationToken.None);

            var second = await _handler.Handle(new GeneratePathologyReportCommand
            {
                HospitalId = hospitalId,
                OrderId = orderId,
                OrderLineId = lineId,
                LoggedInUserName = "tester",
            }, CancellationToken.None);

            Assert.That(second.Success, Is.True, second.Message);
            Assert.That(second.ReportId, Is.EqualTo(first.ReportId));
            Assert.That(second.ReportNo, Is.EqualTo(first.ReportNo));
            Assert.That(_context.PathologyReport.Count(), Is.EqualTo(1));
        }

        [Test]
        public async Task Handle_CalledForDifferentLinesInSameOrder_CreatesDistinctReports()
        {
            var hospitalId = Guid.NewGuid();
            var orderId = SeedOrder(hospitalId);
            var lineOne = SeedLineWithResult(hospitalId, orderId);
            var lineTwo = SeedLineWithResult(hospitalId, orderId);
            _context.SaveChanges();

            var first = await _handler.Handle(new GeneratePathologyReportCommand
            {
                HospitalId = hospitalId,
                OrderId = orderId,
                OrderLineId = lineOne,
                LoggedInUserName = "tester",
            }, CancellationToken.None);

            var second = await _handler.Handle(new GeneratePathologyReportCommand
            {
                HospitalId = hospitalId,
                OrderId = orderId,
                OrderLineId = lineTwo,
                LoggedInUserName = "tester",
            }, CancellationToken.None);

            Assert.That(first.Success, Is.True, first.Message);
            Assert.That(second.Success, Is.True, second.Message);
            Assert.That(second.ReportId, Is.Not.EqualTo(first.ReportId));
            Assert.That(_context.PathologyReport.Count(r => r.OrderId == orderId), Is.EqualTo(2));
        }

        [Test]
        public async Task Handle_BillingPolicyOnReportGeneration_PostsChargeOnFirstGenerateOnly()
        {
            var chargeId = Guid.NewGuid();
            var testId = Guid.NewGuid();
            var encounterId = Guid.NewGuid();
            var (hospitalId, orderId, lineId) = SeedSingleLineOrderWithResult(testIdOverride: testId, encounterId: encounterId);

            _context.BillingPolicy.Add(new BillingPolicy { HospitalId = hospitalId, LabPathTrigger = "ON_REPORT_APPROVAL" });
            _context.ChargeMaster.Add(new ChargeMaster
            {
                ChargeId = chargeId,
                HospitalId = hospitalId,
                DisplayName = "Hemoglobin",
                DefaultRate = 150m,
                IsActive = true,
            });
            _context.PathologyTestMaster.Add(new PathologyTestMaster
            {
                TestId = testId,
                HospitalId = hospitalId,
                TestCode = "HEM-HB",
                TestName = "Hemoglobin",
                ChargeId = chargeId,
                IsActive = true,
            });
            _context.SaveChanges();

            await _handler.Handle(new GeneratePathologyReportCommand
            {
                HospitalId = hospitalId,
                OrderId = orderId,
                OrderLineId = lineId,
                LoggedInUserName = "tester",
            }, CancellationToken.None);

            _mediatorMock.Verify(m => m.Send(
                It.Is<AddChargeEventRequestModel>(r =>
                    r.EncounterId == encounterId &&
                    r.Charges.Count == 1 &&
                    r.Charges.Single().ChargeId == chargeId),
                It.IsAny<CancellationToken>()), Times.Once);

            // Regenerating the same line's report must not post the charge a second time.
            await _handler.Handle(new GeneratePathologyReportCommand
            {
                HospitalId = hospitalId,
                OrderId = orderId,
                OrderLineId = lineId,
                LoggedInUserName = "tester",
            }, CancellationToken.None);

            _mediatorMock.Verify(m => m.Send(It.IsAny<AddChargeEventRequestModel>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task Handle_BillingPolicyOnReportGeneration_SecondLineFirstGenerateAlsoBillsIndependently()
        {
            var chargeIdOne = Guid.NewGuid();
            var chargeIdTwo = Guid.NewGuid();
            var testIdOne = Guid.NewGuid();
            var testIdTwo = Guid.NewGuid();
            var encounterId = Guid.NewGuid();
            var hospitalId = Guid.NewGuid();
            var orderId = SeedOrder(hospitalId, encounterId: encounterId);
            var lineOne = SeedLineWithResult(hospitalId, orderId, testIdOverride: testIdOne);
            var lineTwo = SeedLineWithResult(hospitalId, orderId, testIdOverride: testIdTwo);

            _context.BillingPolicy.Add(new BillingPolicy { HospitalId = hospitalId, LabPathTrigger = "ON_REPORT_APPROVAL" });
            _context.ChargeMaster.AddRange(
                new ChargeMaster { ChargeId = chargeIdOne, HospitalId = hospitalId, DisplayName = "Test One", DefaultRate = 100m, IsActive = true },
                new ChargeMaster { ChargeId = chargeIdTwo, HospitalId = hospitalId, DisplayName = "Test Two", DefaultRate = 200m, IsActive = true });
            _context.PathologyTestMaster.AddRange(
                new PathologyTestMaster { TestId = testIdOne, HospitalId = hospitalId, TestCode = "T1", TestName = "Test One", ChargeId = chargeIdOne, IsActive = true },
                new PathologyTestMaster { TestId = testIdTwo, HospitalId = hospitalId, TestCode = "T2", TestName = "Test Two", ChargeId = chargeIdTwo, IsActive = true });
            _context.SaveChanges();

            await _handler.Handle(new GeneratePathologyReportCommand
            {
                HospitalId = hospitalId,
                OrderId = orderId,
                OrderLineId = lineOne,
                LoggedInUserName = "tester",
            }, CancellationToken.None);

            await _handler.Handle(new GeneratePathologyReportCommand
            {
                HospitalId = hospitalId,
                OrderId = orderId,
                OrderLineId = lineTwo,
                LoggedInUserName = "tester",
            }, CancellationToken.None);

            _mediatorMock.Verify(m => m.Send(
                It.Is<AddChargeEventRequestModel>(r => r.Charges.Count == 1 && r.Charges.Single().ChargeId == chargeIdOne),
                It.IsAny<CancellationToken>()), Times.Once);
            _mediatorMock.Verify(m => m.Send(
                It.Is<AddChargeEventRequestModel>(r => r.Charges.Count == 1 && r.Charges.Single().ChargeId == chargeIdTwo),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task Handle_BillingPolicyNotSetToOnReportGeneration_DoesNotPostCharge()
        {
            var (hospitalId, orderId, lineId) = SeedSingleLineOrderWithResult(encounterId: Guid.NewGuid());
            _context.BillingPolicy.Add(new BillingPolicy { HospitalId = hospitalId, LabPathTrigger = "ON_ORDER" });
            _context.SaveChanges();

            var response = await _handler.Handle(new GeneratePathologyReportCommand
            {
                HospitalId = hospitalId,
                OrderId = orderId,
                OrderLineId = lineId,
                LoggedInUserName = "tester",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            _mediatorMock.Verify(m => m.Send(It.IsAny<AddChargeEventRequestModel>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
