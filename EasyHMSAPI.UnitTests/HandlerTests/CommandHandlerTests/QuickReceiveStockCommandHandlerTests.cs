using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.UnitTests.TestUtils;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class QuickReceiveStockCommandHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<IMediator> _mediatorMock = null!;
        private QuickReceiveStockCommandHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _mediatorMock = new Mock<IMediator>();
            _handler = new QuickReceiveStockCommandHandler(_mediatorMock.Object, _context, NullLogger<QuickReceiveStockCommandHandler>.Instance);
        }

        [TearDown]
        public void TearDown()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        private static QuickReceiveStockRequestModel ValidRequest() => new()
        {
            HospitalId = Guid.NewGuid(),
            StoreId = Guid.NewGuid(),
            InventoryItemId = Guid.NewGuid(),
            Qty = 10,
        };

        [Test]
        public async Task Handle_MissingIds_ReturnsError()
        {
            var response = await _handler.Handle(new QuickReceiveStockRequestModel { Qty = 5 }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("required"));
        }

        [Test]
        public async Task Handle_ZeroQty_ReturnsError()
        {
            var request = ValidRequest();
            request.Qty = 0;

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("greater than zero"));
        }

        [Test]
        public async Task Handle_ExplicitBatchNumber_PassesItThroughUnchanged()
        {
            var request = ValidRequest();
            request.BatchNumber = "LOT-123";
            var batchId = Guid.NewGuid();

            _mediatorMock.Setup(m => m.Send(It.Is<CreateBatchRequestModel>(r => r.BatchNumber == "LOT-123"), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CreateBatchResponseModel { Success = true, BatchId = batchId });
            _mediatorMock.Setup(m => m.Send(It.IsAny<RecordInventoryMovementRequestModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RecordInventoryMovementResponseModel { Success = true, InventoryMovementId = Guid.NewGuid(), NewCurrentStock = 10 });

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.BatchId, Is.EqualTo(batchId));
        }

        [Test]
        public async Task Handle_NoBatchNumber_AutoGeneratesAdhocBatchNumber()
        {
            var request = ValidRequest();
            var batchId = Guid.NewGuid();

            _mediatorMock.Setup(m => m.Send(It.Is<CreateBatchRequestModel>(r => r.BatchNumber.StartsWith("ADHOC-")), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CreateBatchResponseModel { Success = true, BatchId = batchId });
            _mediatorMock.Setup(m => m.Send(It.IsAny<RecordInventoryMovementRequestModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RecordInventoryMovementResponseModel { Success = true, InventoryMovementId = Guid.NewGuid(), NewCurrentStock = 10 });

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            _mediatorMock.Verify(m => m.Send(It.Is<CreateBatchRequestModel>(r => r.BatchNumber.StartsWith("ADHOC-")), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task Handle_BatchCreationFails_ReturnsErrorAndNeverRecordsMovement()
        {
            var request = ValidRequest();

            _mediatorMock.Setup(m => m.Send(It.IsAny<CreateBatchRequestModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CreateBatchResponseModel { Success = false, Message = "Store not found." });

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("Store not found"));
            _mediatorMock.Verify(m => m.Send(It.IsAny<RecordInventoryMovementRequestModel>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task Handle_MovementRecordingFails_ReturnsError()
        {
            var request = ValidRequest();
            var batchId = Guid.NewGuid();

            _mediatorMock.Setup(m => m.Send(It.IsAny<CreateBatchRequestModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CreateBatchResponseModel { Success = true, BatchId = batchId });
            _mediatorMock.Setup(m => m.Send(It.IsAny<RecordInventoryMovementRequestModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RecordInventoryMovementResponseModel { Success = false, Message = "Insufficient stock." });

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("Insufficient stock"));
        }
    }
}
