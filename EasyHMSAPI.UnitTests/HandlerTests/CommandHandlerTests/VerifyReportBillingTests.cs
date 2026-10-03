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
    // The billing policy says "On report approval: post the charge once the pathologist signs off the report". The sign-off
    // is the verification, so that is where the charge is posted; generating or regenerating a draft report never bills.
    [TestFixture]
    public class VerifyReportBillingTests
    {
        private AppDbContext _context = null!;
        private Mock<IMediator> _mediator = null!;
        private VerifyPathologyReportHandler _handler = null!;
        private Guid _hospitalId, _orderId, _encounterId;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _mediator = new Mock<IMediator>();
            _mediator.Setup(m => m.Send(It.IsAny<AddChargeEventRequestModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AddChargeEventResponseModel { Success = true });
            _mediator.Setup(m => m.Send(It.IsAny<CreateDraftInvoiceRequestModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CreateDraftInvoiceResponseModel { Success = true });
            _handler = new VerifyPathologyReportHandler(_context, _mediator.Object);

            _hospitalId = Guid.NewGuid();
            _orderId = Guid.NewGuid();
            _encounterId = Guid.NewGuid();
            _context.PathologyOrder.Add(new PathologyOrder
            {
                OrderId = _orderId, HospitalId = _hospitalId, PatientId = "PTID1", OrderNo = "ORD-1", Status = "COMPLETED", EncounterId = _encounterId,
            });
            _context.SaveChanges();
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private (Guid ReportId, Guid TestId, Guid ChargeId) SeedReportedLine(string testName = "Hemoglobin", decimal rate = 150m)
        {
            var testId = Guid.NewGuid();
            var chargeId = Guid.NewGuid();
            var reportId = Guid.NewGuid();
            _context.ChargeMaster.Add(new ChargeMaster { ChargeId = chargeId, HospitalId = _hospitalId, DisplayName = testName, DefaultRate = rate, IsActive = true });
            _context.PathologyTestMaster.Add(new PathologyTestMaster { TestId = testId, HospitalId = _hospitalId, TestCode = testName[..3], TestName = testName, ChargeId = chargeId, IsActive = true });
            _context.PathologyReport.Add(new PathologyReport { ReportId = reportId, HospitalId = _hospitalId, OrderId = _orderId, ReportNo = "R-" + reportId.ToString()[..4], Status = "GENERATED", GeneratedAt = DateTime.UtcNow });
            _context.PathologyOrderLine.Add(new PathologyOrderLine
            {
                OrderLineId = Guid.NewGuid(), HospitalId = _hospitalId, OrderId = _orderId, TestId = testId, Status = "RESULT_ENTERED", ReportId = reportId,
            });
            _context.SaveChanges();
            return (reportId, testId, chargeId);
        }

        private void SetPolicy(string trigger)
        {
            _context.BillingPolicy.Add(new BillingPolicy { HospitalId = _hospitalId, LabPathTrigger = trigger });
            _context.SaveChanges();
        }

        private Task<VerifyPathologyReportResult> Verify(Guid reportId) => _handler.Handle(new VerifyPathologyReportCommand
        {
            HospitalId = _hospitalId, OrderId = _orderId, ReportId = reportId, PathologistName = "Dr P", PathologistRegNo = "REG1", LoggedInUserName = "Dr P", LoggedInUserId = Guid.NewGuid(),
        }, CancellationToken.None);

        [Test]
        public async Task OnReportApproval_VerifyingPostsTheCharge_AndCreatesTheInvoice()
        {
            SetPolicy("ON_REPORT_APPROVAL");
            var (reportId, _, chargeId) = SeedReportedLine();

            var result = await Verify(reportId);

            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.Message, Is.EqualTo("Report verified."));
            _mediator.Verify(m => m.Send(It.Is<AddChargeEventRequestModel>(r =>
                r.EncounterId == _encounterId && r.Charges.Count == 1 && r.Charges.Single().ChargeId == chargeId && r.Charges.Single().Rate == 150m), It.IsAny<CancellationToken>()), Times.Once);
            _mediator.Verify(m => m.Send(It.IsAny<CreateDraftInvoiceRequestModel>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestCase("OFF")]
        [TestCase("ON_ORDER")]
        [TestCase("ON_SAMPLE_COLLECTION")]
        public async Task OtherTriggers_VerifyingDoesNotBill(string trigger)
        {
            SetPolicy(trigger);
            var (reportId, _, _) = SeedReportedLine();

            var result = await Verify(reportId);

            Assert.That(result.Success, Is.True, result.Message);
            _mediator.Verify(m => m.Send(It.IsAny<AddChargeEventRequestModel>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task NoPolicyRow_VerifyingDoesNotBill()
        {
            var (reportId, _, _) = SeedReportedLine();
            Assert.That((await Verify(reportId)).Success, Is.True);
            _mediator.Verify(m => m.Send(It.IsAny<AddChargeEventRequestModel>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task ReVerifyingAfterAnAmendment_DoesNotBillTwice()
        {
            SetPolicy("ON_REPORT_APPROVAL");
            var (reportId, testId, chargeId) = SeedReportedLine();
            await Verify(reportId);

            // the first charge is now on the bill; an amendment resets the report and it is verified again
            _context.BillingChargeEvent.Add(new BillingChargeEvent
            {
                ChargeEventId = Guid.NewGuid(), HospitalId = _hospitalId, EncounterId = _encounterId, ChargeId = chargeId,
                SourceRefId = $"{_orderId}:{testId}", DisplayName = "Hemoglobin", Qty = 1, UnitPrice = 150m, NetAmount = 150m,
            });
            var report = _context.PathologyReport.Single();
            report.Status = "AMENDED"; report.ApprovedAt = null;
            _context.SaveChanges();

            var again = await Verify(reportId);

            Assert.That(again.Success, Is.True, again.Message);
            _mediator.Verify(m => m.Send(It.IsAny<AddChargeEventRequestModel>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task EachReportBillsOnlyItsOwnTest()
        {
            SetPolicy("ON_REPORT_APPROVAL");
            var (reportOne, _, chargeOne) = SeedReportedLine("Hemoglobin", 100m);
            var (reportTwo, _, chargeTwo) = SeedReportedLine("Glucose", 200m);

            await Verify(reportOne);
            _mediator.Verify(m => m.Send(It.Is<AddChargeEventRequestModel>(r => r.Charges.Single().ChargeId == chargeOne), It.IsAny<CancellationToken>()), Times.Once);
            _mediator.Verify(m => m.Send(It.Is<AddChargeEventRequestModel>(r => r.Charges.Single().ChargeId == chargeTwo), It.IsAny<CancellationToken>()), Times.Never);

            await Verify(reportTwo);
            _mediator.Verify(m => m.Send(It.Is<AddChargeEventRequestModel>(r => r.Charges.Single().ChargeId == chargeTwo), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task BillingFailure_DoesNotUndoTheVerification_AndIsReportedToTheUser()
        {
            SetPolicy("ON_REPORT_APPROVAL");
            var (reportId, _, _) = SeedReportedLine();
            _mediator.Setup(m => m.Send(It.IsAny<AddChargeEventRequestModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AddChargeEventResponseModel { Success = false, Message = "boom" });

            var result = await Verify(reportId);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Message, Does.Contain("Report verified.").And.Contain("Billing tab"));
            Assert.That(_context.PathologyReport.Single().Status, Is.EqualTo("VERIFIED"));
        }

        [Test]
        public async Task OrderWithoutABillingEncounter_WarnsInsteadOfSilentlySkipping()
        {
            SetPolicy("ON_REPORT_APPROVAL");
            var (reportId, _, _) = SeedReportedLine();
            var order = _context.PathologyOrder.Single();
            order.EncounterId = null;
            _context.SaveChanges();

            var result = await Verify(reportId);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Message, Does.Contain("no billing encounter"));
            _mediator.Verify(m => m.Send(It.IsAny<AddChargeEventRequestModel>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
