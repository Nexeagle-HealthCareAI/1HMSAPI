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
    // Covers the previously-missing PENDING -> SAMPLE_COLLECTED transition -- PathologyOrderLine.Status
    // always had room for it, but no handler anywhere ever set it, so ON_SAMPLE_COLLECTION billing
    // (referenced in the original 1Lab plan) had no hook to attach to until this handler existed.
    [TestFixture]
    public class CollectPathologySampleHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<IMediator> _mediatorMock = null!;
        private CollectPathologySampleHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _mediatorMock = new Mock<IMediator>();
            _mediatorMock
                .Setup(m => m.Send(It.IsAny<AddChargeEventRequestModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AddChargeEventResponseModel { Success = true });
            _handler = new CollectPathologySampleHandler(_context, _mediatorMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        private (PathologyOrder Order, PathologyOrderLine Line) SeedPendingLine(Guid hospitalId, Guid? encounterId = null, string orderStatus = "PLACED")
        {
            var testId = Guid.NewGuid();
            var orderId = Guid.NewGuid();
            var orderLineId = Guid.NewGuid();

            _context.PathologyTestMaster.Add(new PathologyTestMaster
            {
                TestId = testId,
                HospitalId = hospitalId,
                TestCode = "HEM-CBC",
                TestName = "Complete Blood Count",
                IsActive = true,
            });
            var order = new PathologyOrder
            {
                OrderId = orderId,
                HospitalId = hospitalId,
                PatientId = "PTID00000001",
                EncounterId = encounterId,
                OrderNo = "LR-1",
                OrderDate = DateTime.UtcNow,
                Status = orderStatus,
            };
            _context.PathologyOrder.Add(order);
            var line = new PathologyOrderLine
            {
                OrderLineId = orderLineId,
                HospitalId = hospitalId,
                OrderId = orderId,
                TestId = testId,
                Status = "PENDING",
            };
            _context.PathologyOrderLine.Add(line);
            _context.SaveChanges();

            return (order, line);
        }

        [Test]
        public async Task Handle_PendingLine_TransitionsToSampleCollectedAndSetsTimestamp()
        {
            var hospitalId = Guid.NewGuid();
            var (_, line) = SeedPendingLine(hospitalId);

            var result = await _handler.Handle(new CollectPathologySampleCommand
            {
                HospitalId = hospitalId,
                OrderId = line.OrderId,
                OrderLineId = line.OrderLineId,
                SampleBarcode = "BARCODE-001",
                LoggedInUserName = "tech1",
            }, CancellationToken.None);

            Assert.That(result, Is.True);
            var saved = _context.PathologyOrderLine.Single(l => l.OrderLineId == line.OrderLineId);
            Assert.That(saved.Status, Is.EqualTo("SAMPLE_COLLECTED"));
            Assert.That(saved.SampleCollectedAt, Is.Not.Null);
            Assert.That(saved.SampleBarcode, Is.EqualTo("BARCODE-001"));
        }

        [Test]
        public async Task Handle_FirstLineCollected_OrderTransitionsFromPlacedToInProgress()
        {
            var hospitalId = Guid.NewGuid();
            var (order, line) = SeedPendingLine(hospitalId, orderStatus: "PLACED");

            await _handler.Handle(new CollectPathologySampleCommand
            {
                HospitalId = hospitalId,
                OrderId = line.OrderId,
                OrderLineId = line.OrderLineId,
            }, CancellationToken.None);

            var savedOrder = _context.PathologyOrder.Single(o => o.OrderId == order.OrderId);
            Assert.That(savedOrder.Status, Is.EqualTo("IN_PROGRESS"));
        }

        [Test]
        public async Task Handle_LineAlreadyPastPending_ReturnsFalseWithoutChanges()
        {
            var hospitalId = Guid.NewGuid();
            var (_, line) = SeedPendingLine(hospitalId);
            line.Status = "RESULT_ENTERED";
            _context.SaveChanges();

            var result = await _handler.Handle(new CollectPathologySampleCommand
            {
                HospitalId = hospitalId,
                OrderId = line.OrderId,
                OrderLineId = line.OrderLineId,
            }, CancellationToken.None);

            Assert.That(result, Is.False);
            var saved = _context.PathologyOrderLine.Single(l => l.OrderLineId == line.OrderLineId);
            Assert.That(saved.Status, Is.EqualTo("RESULT_ENTERED"));
        }

        [Test]
        public async Task Handle_OrderLineNotFound_ReturnsFalse()
        {
            var result = await _handler.Handle(new CollectPathologySampleCommand
            {
                HospitalId = Guid.NewGuid(),
                OrderId = Guid.NewGuid(),
                OrderLineId = Guid.NewGuid(),
            }, CancellationToken.None);

            Assert.That(result, Is.False);
        }

        [Test]
        public async Task Handle_TriggerSetToOnSampleCollection_PostsCharge()
        {
            var hospitalId = Guid.NewGuid();
            var encounterId = Guid.NewGuid();
            var (order, line) = SeedPendingLine(hospitalId, encounterId);
            var chargeId = Guid.NewGuid();
            _context.ChargeMaster.Add(new ChargeMaster { ChargeId = chargeId, HospitalId = hospitalId, DisplayName = "CBC", DefaultRate = 150m, IsActive = true });
            var test = _context.PathologyTestMaster.Single(t => t.TestId == line.TestId);
            test.ChargeId = chargeId;
            _context.BillingPolicy.Add(new BillingPolicy { HospitalId = hospitalId, LabPathTrigger = "ON_SAMPLE_COLLECTION" });
            _context.SaveChanges();

            await _handler.Handle(new CollectPathologySampleCommand
            {
                HospitalId = hospitalId,
                OrderId = line.OrderId,
                OrderLineId = line.OrderLineId,
            }, CancellationToken.None);

            _mediatorMock.Verify(m => m.Send(
                It.Is<AddChargeEventRequestModel>(r =>
                    r.EncounterId == encounterId &&
                    r.Charges.Count == 1 &&
                    r.Charges.Single().ChargeId == chargeId &&
                    r.Charges.Single().Rate == 150m),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task Handle_TriggerNotSetToOnSampleCollection_DoesNotPostCharge()
        {
            var hospitalId = Guid.NewGuid();
            var (_, line) = SeedPendingLine(hospitalId, Guid.NewGuid());
            _context.BillingPolicy.Add(new BillingPolicy { HospitalId = hospitalId, LabPathTrigger = "ON_ORDER" });
            _context.SaveChanges();

            await _handler.Handle(new CollectPathologySampleCommand
            {
                HospitalId = hospitalId,
                OrderId = line.OrderId,
                OrderLineId = line.OrderLineId,
            }, CancellationToken.None);

            _mediatorMock.Verify(m => m.Send(It.IsAny<AddChargeEventRequestModel>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
