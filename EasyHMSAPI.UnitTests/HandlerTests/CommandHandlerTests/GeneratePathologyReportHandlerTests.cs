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
    // Covers the report-workflow simplification: there is no longer a separate technician-sign /
    // pathologist-approve step (both handlers were removed) -- this is the single, freely
    // re-callable "generate/update report" action. Regenerating an order that already has a report
    // must reuse it (same ReportId/ReportNo) rather than reject, and must NOT re-dispatch the
    // ON_REPORT_APPROVAL billing trigger a second time -- AddChargeEventHandler has no dedup for
    // this caller, so firing it again on every "Update Report" click would double-bill the same
    // tests.
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

        private (Guid HospitalId, Guid OrderId, Guid LineId) SeedOrderWithResult(
            Guid? chargeId = null, Guid? testIdOverride = null, string lineStatus = "RESULT_ENTERED", Guid? encounterId = null)
        {
            var hospitalId = Guid.NewGuid();
            var orderId = Guid.NewGuid();
            var lineId = Guid.NewGuid();
            var testId = testIdOverride ?? Guid.NewGuid();

            _context.PathologyOrder.Add(new PathologyOrder
            {
                OrderId = orderId,
                HospitalId = hospitalId,
                PatientId = "PTID00000001",
                OrderNo = "ORD-1",
                Status = "IN_PROGRESS",
                EncounterId = encounterId,
            });
            _context.PathologyOrderLine.Add(new PathologyOrderLine
            {
                OrderLineId = lineId,
                HospitalId = hospitalId,
                OrderId = orderId,
                TestId = testId,
                Status = lineStatus,
            });
            _context.PathologyResult.Add(new PathologyResult
            {
                ResultId = Guid.NewGuid(),
                HospitalId = hospitalId,
                OrderLineId = lineId,
                ResultValuesJson = "{\"Hemoglobin\":{\"value\":\"13.5\",\"flag\":\"NORMAL\"}}",
            });
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
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
        }

        [Test]
        public async Task Handle_SomeLinesMissingResults_ReturnsFailure()
        {
            var (hospitalId, orderId, _) = SeedOrderWithResult();
            _context.PathologyOrderLine.Add(new PathologyOrderLine
            {
                OrderLineId = Guid.NewGuid(),
                HospitalId = hospitalId,
                OrderId = orderId,
                TestId = Guid.NewGuid(),
                Status = "PENDING",
            });
            _context.SaveChanges();

            var response = await _handler.Handle(new GeneratePathologyReportCommand
            {
                HospitalId = hospitalId,
                OrderId = orderId,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(_context.PathologyReport.Any(r => r.OrderId == orderId), Is.False);
        }

        [Test]
        public async Task Handle_AllResultsEntered_CreatesReportAndCompletesOrder()
        {
            var (hospitalId, orderId, lineId) = SeedOrderWithResult();

            var response = await _handler.Handle(new GeneratePathologyReportCommand
            {
                HospitalId = hospitalId,
                OrderId = orderId,
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
        public async Task Handle_CalledAgainForSameOrder_ReusesExistingReportInstead()
        {
            var (hospitalId, orderId, _) = SeedOrderWithResult();

            var first = await _handler.Handle(new GeneratePathologyReportCommand
            {
                HospitalId = hospitalId,
                OrderId = orderId,
                LoggedInUserName = "tester",
            }, CancellationToken.None);

            var second = await _handler.Handle(new GeneratePathologyReportCommand
            {
                HospitalId = hospitalId,
                OrderId = orderId,
                LoggedInUserName = "tester",
            }, CancellationToken.None);

            Assert.That(second.Success, Is.True, second.Message);
            Assert.That(second.ReportId, Is.EqualTo(first.ReportId));
            Assert.That(second.ReportNo, Is.EqualTo(first.ReportNo));
            Assert.That(_context.PathologyReport.Count(r => r.OrderId == orderId), Is.EqualTo(1));
        }

        [Test]
        public async Task Handle_BillingPolicyOnReportGeneration_PostsChargeOnFirstGenerateOnly()
        {
            var chargeId = Guid.NewGuid();
            var testId = Guid.NewGuid();
            var encounterId = Guid.NewGuid();
            var (hospitalId, orderId, _) = SeedOrderWithResult(testIdOverride: testId, encounterId: encounterId);

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
                LoggedInUserName = "tester",
            }, CancellationToken.None);

            _mediatorMock.Verify(m => m.Send(
                It.Is<AddChargeEventRequestModel>(r =>
                    r.EncounterId == encounterId &&
                    r.Charges.Count == 1 &&
                    r.Charges.Single().ChargeId == chargeId),
                It.IsAny<CancellationToken>()), Times.Once);

            // Regenerating the same report must not post the charge a second time.
            await _handler.Handle(new GeneratePathologyReportCommand
            {
                HospitalId = hospitalId,
                OrderId = orderId,
                LoggedInUserName = "tester",
            }, CancellationToken.None);

            _mediatorMock.Verify(m => m.Send(It.IsAny<AddChargeEventRequestModel>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task Handle_BillingPolicyNotSetToOnReportGeneration_DoesNotPostCharge()
        {
            var (hospitalId, orderId, _) = SeedOrderWithResult(encounterId: Guid.NewGuid());
            _context.BillingPolicy.Add(new BillingPolicy { HospitalId = hospitalId, LabPathTrigger = "ON_ORDER" });
            _context.SaveChanges();

            var response = await _handler.Handle(new GeneratePathologyReportCommand
            {
                HospitalId = hospitalId,
                OrderId = orderId,
                LoggedInUserName = "tester",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            _mediatorMock.Verify(m => m.Send(It.IsAny<AddChargeEventRequestModel>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
