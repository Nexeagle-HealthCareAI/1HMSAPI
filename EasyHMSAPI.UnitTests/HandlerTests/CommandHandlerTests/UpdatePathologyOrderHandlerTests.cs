using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using MediatR;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    // Full order editing is allowed at any point in an order's progress (confirmed via clarifying
    // question) -- these tests cover the reconciliation that unlocks: removing an already-reported
    // line deletes the report, reassigning the patient invalidates surviving reports and voids/rebills
    // charges, and adding a test to an in-progress order bills just that one test.
    [TestFixture]
    public class UpdatePathologyOrderHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<IMediator> _mediatorMock = null!;
        private UpdatePathologyOrderHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _mediatorMock = new Mock<IMediator>();
            _mediatorMock
                .Setup(m => m.Send(It.IsAny<AddChargeEventRequestModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AddChargeEventResponseModel { Success = true });
            _mediatorMock
                .Setup(m => m.Send(It.IsAny<CreateDraftInvoiceRequestModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CreateDraftInvoiceResponseModel { Success = true });
            _handler = new UpdatePathologyOrderHandler(_context, _mediatorMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        private (Guid HospitalId, Guid OrderId, Guid TestId, Guid ChargeId) SeedOrderWithOneTest(string patientId = "PTID00000001", string orderStatus = "PLACED", string labPathTrigger = "ON_ORDER")
        {
            var hospitalId = Guid.NewGuid();
            var orderId = Guid.NewGuid();
            var chargeId = Guid.NewGuid();
            var testId = Guid.NewGuid();
            var encounterId = Guid.NewGuid();

            _context.BillingPolicy.Add(new BillingPolicy { HospitalId = hospitalId, LabPathTrigger = labPathTrigger });
            _context.ChargeMaster.Add(new ChargeMaster { ChargeId = chargeId, HospitalId = hospitalId, DisplayName = "CBC", DefaultRate = 250m, IsActive = true });
            _context.PathologyTestMaster.Add(new PathologyTestMaster { TestId = testId, HospitalId = hospitalId, TestCode = "HEM-CBC", TestName = "Complete Blood Count (CBC)", ChargeId = chargeId, IsActive = true });
            _context.PathologyOrder.Add(new PathologyOrder
            {
                OrderId = orderId,
                HospitalId = hospitalId,
                PatientId = patientId,
                EncounterId = encounterId,
                OrderNo = "ORD-1",
                Status = orderStatus,
                SourceType = "OPD",
            });
            _context.PathologyOrderLine.Add(new PathologyOrderLine
            {
                OrderLineId = Guid.NewGuid(),
                HospitalId = hospitalId,
                OrderId = orderId,
                TestId = testId,
                Status = "PENDING",
            });
            // The handler's cross-tenant ownership check (PatientId must belong to HospitalId)
            // needs a real PatientRegistration row -- seed both ids this file's tests use
            // (PTID00000001 is the order's own patient; PTID00000002 is used as a reassignment
            // target in the patient-switch test) rather than just the bare `patientId` param.
            _context.PatientRegistrations.Add(new PatientRegistration { RegistrationId = Guid.NewGuid(), HospitalId = hospitalId, PatientId = "PTID00000001", FullName = "Test Patient", RegisteredAt = DateTime.UtcNow });
            _context.PatientRegistrations.Add(new PatientRegistration { RegistrationId = Guid.NewGuid(), HospitalId = hospitalId, PatientId = "PTID00000002", FullName = "Test Patient 2", RegisteredAt = DateTime.UtcNow });
            _context.SaveChanges();

            return (hospitalId, orderId, testId, chargeId);
        }

        private UpdatePathologyOrderCommand BaseCommand(Guid hospitalId, Guid orderId, string patientId, Guid encounterId, params Guid[] testIds) => new()
        {
            HospitalId = hospitalId,
            OrderId = orderId,
            PatientId = patientId,
            EncounterId = encounterId,
            TestIds = testIds.ToList(),
            SourceType = "OPD",
            LoggedInUserName = "tester",
        };

        [Test]
        public async Task Handle_OrderNotFound_ReturnsFailure()
        {
            var response = await _handler.Handle(new UpdatePathologyOrderCommand
            {
                HospitalId = Guid.NewGuid(),
                OrderId = Guid.NewGuid(),
                PatientId = "PTID00000001",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
        }

        [Test]
        public async Task Handle_OrderCancelled_ReturnsFailure()
        {
            var (hospitalId, orderId, testId, _) = SeedOrderWithOneTest(orderStatus: "CANCELLED");
            var order = _context.PathologyOrder.Single(o => o.OrderId == orderId);

            var response = await _handler.Handle(BaseCommand(hospitalId, orderId, "PTID00000001", order.EncounterId!.Value, testId), CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("cancelled"));
        }

        [Test]
        public async Task Handle_TestIdNotInHospitalCatalog_ReturnsFailure()
        {
            var (hospitalId, orderId, testId, _) = SeedOrderWithOneTest();
            var order = _context.PathologyOrder.Single(o => o.OrderId == orderId);
            var foreignTestId = Guid.NewGuid();

            var response = await _handler.Handle(BaseCommand(hospitalId, orderId, "PTID00000001", order.EncounterId!.Value, testId, foreignTestId), CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("not in this hospital's catalog"));
        }

        [Test]
        public async Task Handle_AddTestToInProgressOrder_CreatesPendingLineAndBillsOnlyTheNewTest()
        {
            var (hospitalId, orderId, testId, _) = SeedOrderWithOneTest();
            var order = _context.PathologyOrder.Single(o => o.OrderId == orderId);
            var existingLine = _context.PathologyOrderLine.Single(l => l.OrderId == orderId);
            existingLine.Status = "SAMPLE_COLLECTED"; // order already in progress
            _context.SaveChanges();

            var newChargeId = Guid.NewGuid();
            var newTestId = Guid.NewGuid();
            _context.ChargeMaster.Add(new ChargeMaster { ChargeId = newChargeId, HospitalId = hospitalId, DisplayName = "LFT", DefaultRate = 400m, IsActive = true });
            _context.PathologyTestMaster.Add(new PathologyTestMaster { TestId = newTestId, HospitalId = hospitalId, TestCode = "BIO-LFT", TestName = "LFT", ChargeId = newChargeId, IsActive = true });
            _context.SaveChanges();

            var response = await _handler.Handle(BaseCommand(hospitalId, orderId, "PTID00000001", order.EncounterId!.Value, testId, newTestId), CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            var lines = _context.PathologyOrderLine.Where(l => l.OrderId == orderId).ToList();
            Assert.That(lines, Has.Count.EqualTo(2));
            Assert.That(lines.Single(l => l.TestId == newTestId).Status, Is.EqualTo("PENDING"));
            _mediatorMock.Verify(m => m.Send(
                It.Is<AddChargeEventRequestModel>(r => r.Charges.Count == 1 && r.Charges.Single().ChargeId == newChargeId),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task Handle_AddTestToInProgressOrder_AlsoCreatesDraftInvoice()
        {
            var (hospitalId, orderId, testId, _) = SeedOrderWithOneTest();
            var order = _context.PathologyOrder.Single(o => o.OrderId == orderId);

            var newChargeId = Guid.NewGuid();
            var newTestId = Guid.NewGuid();
            _context.ChargeMaster.Add(new ChargeMaster { ChargeId = newChargeId, HospitalId = hospitalId, DisplayName = "LFT", DefaultRate = 400m, IsActive = true });
            _context.PathologyTestMaster.Add(new PathologyTestMaster { TestId = newTestId, HospitalId = hospitalId, TestCode = "BIO-LFT", TestName = "LFT", ChargeId = newChargeId, IsActive = true });
            _context.SaveChanges();

            var response = await _handler.Handle(BaseCommand(hospitalId, orderId, "PTID00000001", order.EncounterId!.Value, testId, newTestId), CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.BillingWarning, Is.Null);
            _mediatorMock.Verify(m => m.Send(
                It.Is<CreateDraftInvoiceRequestModel>(r => r.EncounterId == order.EncounterId!.Value),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task Handle_AddTestButBillingPostFails_UpdateStillSucceedsButBillingWarningIsSet()
        {
            var (hospitalId, orderId, testId, _) = SeedOrderWithOneTest();
            var order = _context.PathologyOrder.Single(o => o.OrderId == orderId);

            var newChargeId = Guid.NewGuid();
            var newTestId = Guid.NewGuid();
            _context.ChargeMaster.Add(new ChargeMaster { ChargeId = newChargeId, HospitalId = hospitalId, DisplayName = "LFT", DefaultRate = 400m, IsActive = true });
            _context.PathologyTestMaster.Add(new PathologyTestMaster { TestId = newTestId, HospitalId = hospitalId, TestCode = "BIO-LFT", TestName = "LFT", ChargeId = newChargeId, IsActive = true });
            _context.SaveChanges();

            _mediatorMock
                .Setup(m => m.Send(It.IsAny<AddChargeEventRequestModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AddChargeEventResponseModel { Success = false, Message = "Encounter is not open (current status: FINALIZED)." });

            var response = await _handler.Handle(BaseCommand(hospitalId, orderId, "PTID00000001", order.EncounterId!.Value, testId, newTestId), CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.BillingWarning, Does.Contain("Encounter is not open"));
        }

        [Test]
        public async Task Handle_RemovePendingLineWithNoCharge_DeletesLineWithoutVoiding()
        {
            var (hospitalId, orderId, testId, _) = SeedOrderWithOneTest(labPathTrigger: "ON_REPORT_APPROVAL");
            var order = _context.PathologyOrder.Single(o => o.OrderId == orderId);

            var response = await _handler.Handle(BaseCommand(hospitalId, orderId, "PTID00000001", order.EncounterId!.Value), CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            Assert.That(_context.PathologyOrderLine.Any(l => l.OrderId == orderId), Is.False);
        }

        [Test]
        public async Task Handle_RemoveLineWithReport_DeletesReportAndResultAndVoidsItsCharge()
        {
            var (hospitalId, orderId, testId, chargeId) = SeedOrderWithOneTest();
            var order = _context.PathologyOrder.Single(o => o.OrderId == orderId);
            var line = _context.PathologyOrderLine.Single(l => l.OrderId == orderId);
            var reportId = Guid.NewGuid();
            _context.PathologyReport.Add(new PathologyReport { ReportId = reportId, HospitalId = hospitalId, OrderId = orderId, ReportNo = "LR-1", Status = "GENERATED", GeneratedAt = DateTime.UtcNow });
            _context.PathologyResult.Add(new PathologyResult { ResultId = Guid.NewGuid(), HospitalId = hospitalId, OrderLineId = line.OrderLineId, ReportId = reportId, ResultValuesJson = "{}" });
            line.ReportId = reportId;
            line.Status = "RESULT_ENTERED";
            var chargeEventId = Guid.NewGuid();
            _context.BillingChargeEvent.Add(new BillingChargeEvent
            {
                ChargeEventId = chargeEventId,
                HospitalId = hospitalId,
                EncounterId = order.EncounterId!.Value,
                SourceModule = BillingConstants.SourceModule.LabPath,
                SourceRefId = orderId.ToString(),
                ChargeId = chargeId,
                StatusCode = BillingConstants.ChargeEventStatus.Posted,
                NetAmount = 250m,
                Qty = 1,
                UnitPrice = 250m,
            });
            _context.SaveChanges();

            var response = await _handler.Handle(BaseCommand(hospitalId, orderId, "PTID00000001", order.EncounterId!.Value), CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            Assert.That(_context.PathologyOrderLine.Any(l => l.OrderId == orderId), Is.False);
            Assert.That(_context.PathologyReport.Any(r => r.ReportId == reportId), Is.False);
            Assert.That(_context.PathologyResult.Any(r => r.OrderLineId == line.OrderLineId), Is.False);
            var voidedCharge = _context.BillingChargeEvent.Single(c => c.ChargeEventId == chargeEventId);
            Assert.That(voidedCharge.StatusCode, Is.EqualTo(BillingConstants.ChargeEventStatus.Void));
        }

        [Test]
        public async Task Handle_ReassignPatientOnOrderWithGeneratedReport_InvalidatesReportKeepsResultAndVoidsThenRebillsCharge()
        {
            var (hospitalId, orderId, testId, chargeId) = SeedOrderWithOneTest();
            var order = _context.PathologyOrder.Single(o => o.OrderId == orderId);
            var line = _context.PathologyOrderLine.Single(l => l.OrderId == orderId);
            var reportId = Guid.NewGuid();
            _context.PathologyReport.Add(new PathologyReport { ReportId = reportId, HospitalId = hospitalId, OrderId = orderId, ReportNo = "LR-1", Status = "GENERATED", GeneratedAt = DateTime.UtcNow });
            _context.PathologyResult.Add(new PathologyResult { ResultId = Guid.NewGuid(), HospitalId = hospitalId, OrderLineId = line.OrderLineId, ReportId = reportId, ResultValuesJson = "{\"Hb\":\"14\"}" });
            line.ReportId = reportId;
            line.Status = "RESULT_ENTERED";
            var chargeEventId = Guid.NewGuid();
            _context.BillingChargeEvent.Add(new BillingChargeEvent
            {
                ChargeEventId = chargeEventId,
                HospitalId = hospitalId,
                EncounterId = order.EncounterId!.Value,
                SourceModule = BillingConstants.SourceModule.LabPath,
                SourceRefId = orderId.ToString(),
                ChargeId = chargeId,
                StatusCode = BillingConstants.ChargeEventStatus.Posted,
                NetAmount = 250m,
                Qty = 1,
                UnitPrice = 250m,
            });
            _context.SaveChanges();

            var newEncounterId = Guid.NewGuid();
            var response = await _handler.Handle(BaseCommand(hospitalId, orderId, "PTID00000002", newEncounterId, testId), CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            var survivingLine = _context.PathologyOrderLine.Single(l => l.OrderId == orderId);
            Assert.That(survivingLine.ReportId, Is.Null);
            Assert.That(_context.PathologyReport.Any(r => r.ReportId == reportId), Is.False);
            // Result is kept -- only the rendered report is discarded.
            Assert.That(_context.PathologyResult.Any(r => r.OrderLineId == line.OrderLineId), Is.True);
            var updatedOrder = _context.PathologyOrder.Single(o => o.OrderId == orderId);
            Assert.That(updatedOrder.PatientId, Is.EqualTo("PTID00000002"));
            Assert.That(updatedOrder.EncounterId, Is.EqualTo(newEncounterId));
            var oldCharge = _context.BillingChargeEvent.Single(c => c.ChargeEventId == chargeEventId);
            Assert.That(oldCharge.StatusCode, Is.EqualTo(BillingConstants.ChargeEventStatus.Void));
            _mediatorMock.Verify(m => m.Send(
                It.Is<AddChargeEventRequestModel>(r => r.PatientId == "PTID00000002" && r.EncounterId == newEncounterId && r.Charges.Count == 1),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task Handle_SamePatientAndContext_DoesNotVoidExistingCharges()
        {
            var (hospitalId, orderId, testId, chargeId) = SeedOrderWithOneTest();
            var order = _context.PathologyOrder.Single(o => o.OrderId == orderId);
            var chargeEventId = Guid.NewGuid();
            _context.BillingChargeEvent.Add(new BillingChargeEvent
            {
                ChargeEventId = chargeEventId,
                HospitalId = hospitalId,
                EncounterId = order.EncounterId!.Value,
                SourceModule = BillingConstants.SourceModule.LabPath,
                SourceRefId = orderId.ToString(),
                ChargeId = chargeId,
                StatusCode = BillingConstants.ChargeEventStatus.Posted,
                NetAmount = 250m,
                Qty = 1,
                UnitPrice = 250m,
            });
            _context.SaveChanges();

            var response = await _handler.Handle(BaseCommand(hospitalId, orderId, "PTID00000001", order.EncounterId!.Value, testId), CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            var charge = _context.BillingChargeEvent.Single(c => c.ChargeEventId == chargeEventId);
            Assert.That(charge.StatusCode, Is.EqualTo(BillingConstants.ChargeEventStatus.Posted));
            _mediatorMock.Verify(m => m.Send(It.IsAny<AddChargeEventRequestModel>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task Handle_RemovingOneTest_DoesNotVoidSiblingTestChargeSharingSameChargeId()
        {
            // Two different tests can be mapped to the same ChargeMaster row (nothing in the catalog
            // prevents it) -- removing one must not void the other's still-owed charge just because
            // they share a ChargeId. Regression test for the cross-void bug found in the pathology
            // billing audit: void logic used to key only on ChargeId, not on the specific test/line.
            var hospitalId = Guid.NewGuid();
            var orderId = Guid.NewGuid();
            var sharedChargeId = Guid.NewGuid();
            var keptTestId = Guid.NewGuid();
            var removedTestId = Guid.NewGuid();
            var encounterId = Guid.NewGuid();

            _context.BillingPolicy.Add(new BillingPolicy { HospitalId = hospitalId, LabPathTrigger = "ON_ORDER" });
            _context.ChargeMaster.Add(new ChargeMaster { ChargeId = sharedChargeId, HospitalId = hospitalId, DisplayName = "Blood Draw", DefaultRate = 100m, IsActive = true });
            _context.PathologyTestMaster.Add(new PathologyTestMaster { TestId = keptTestId, HospitalId = hospitalId, TestCode = "T-KEEP", TestName = "Kept Test", ChargeId = sharedChargeId, IsActive = true });
            _context.PathologyTestMaster.Add(new PathologyTestMaster { TestId = removedTestId, HospitalId = hospitalId, TestCode = "T-REMOVE", TestName = "Removed Test", ChargeId = sharedChargeId, IsActive = true });
            _context.PathologyOrder.Add(new PathologyOrder
            {
                OrderId = orderId,
                HospitalId = hospitalId,
                PatientId = "PTID00000001",
                EncounterId = encounterId,
                OrderNo = "ORD-2",
                Status = "PLACED",
                SourceType = "OPD",
            });
            _context.PathologyOrderLine.Add(new PathologyOrderLine { OrderLineId = Guid.NewGuid(), HospitalId = hospitalId, OrderId = orderId, TestId = keptTestId, Status = "PENDING" });
            _context.PathologyOrderLine.Add(new PathologyOrderLine { OrderLineId = Guid.NewGuid(), HospitalId = hospitalId, OrderId = orderId, TestId = removedTestId, Status = "PENDING" });
            _context.PatientRegistrations.Add(new PatientRegistration { RegistrationId = Guid.NewGuid(), HospitalId = hospitalId, PatientId = "PTID00000001", FullName = "Test Patient", RegisteredAt = DateTime.UtcNow });

            var keptChargeEventId = Guid.NewGuid();
            var removedChargeEventId = Guid.NewGuid();
            _context.BillingChargeEvent.Add(new BillingChargeEvent
            {
                ChargeEventId = keptChargeEventId, HospitalId = hospitalId, EncounterId = encounterId,
                SourceModule = BillingConstants.SourceModule.LabPath, SourceRefId = $"{orderId}:{keptTestId}",
                ChargeId = sharedChargeId, StatusCode = BillingConstants.ChargeEventStatus.Posted,
                NetAmount = 100m, Qty = 1, UnitPrice = 100m,
            });
            _context.BillingChargeEvent.Add(new BillingChargeEvent
            {
                ChargeEventId = removedChargeEventId, HospitalId = hospitalId, EncounterId = encounterId,
                SourceModule = BillingConstants.SourceModule.LabPath, SourceRefId = $"{orderId}:{removedTestId}",
                ChargeId = sharedChargeId, StatusCode = BillingConstants.ChargeEventStatus.Posted,
                NetAmount = 100m, Qty = 1, UnitPrice = 100m,
            });
            _context.SaveChanges();

            // Edit the order to keep only keptTestId -- removes removedTestId.
            var response = await _handler.Handle(BaseCommand(hospitalId, orderId, "PTID00000001", encounterId, keptTestId), CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            var keptCharge = _context.BillingChargeEvent.Single(c => c.ChargeEventId == keptChargeEventId);
            var removedCharge = _context.BillingChargeEvent.Single(c => c.ChargeEventId == removedChargeEventId);
            Assert.That(keptCharge.StatusCode, Is.EqualTo(BillingConstants.ChargeEventStatus.Posted), "sibling test's charge must survive");
            Assert.That(removedCharge.StatusCode, Is.EqualTo(BillingConstants.ChargeEventStatus.Void));
        }

        [Test]
        public async Task Handle_UpdatesNotesAndStatFlagUnconditionally()
        {
            var (hospitalId, orderId, testId, _) = SeedOrderWithOneTest();
            var order = _context.PathologyOrder.Single(o => o.OrderId == orderId);
            var command = BaseCommand(hospitalId, orderId, "PTID00000001", order.EncounterId!.Value, testId);
            command.Notes = "Fasting sample";
            command.IsStat = true;

            var response = await _handler.Handle(command, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            var updated = _context.PathologyOrder.Single(o => o.OrderId == orderId);
            Assert.That(updated.Notes, Is.EqualTo("Fasting sample"));
            Assert.That(updated.IsStat, Is.True);
        }
    }
}
