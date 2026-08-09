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
    public class TransferStockCommandHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<IMediator> _mediatorMock = null!;
        private TransferStockCommandHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _mediatorMock = new Mock<IMediator>();
            _handler = new TransferStockCommandHandler(_mediatorMock.Object, _context, NullLogger<TransferStockCommandHandler>.Instance);
        }

        [TearDown]
        public void TearDown()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        private static TransferStockRequestModel ValidRequest() => new()
        {
            HospitalId = Guid.NewGuid(),
            InventoryItemId = Guid.NewGuid(),
            FromStoreId = Guid.NewGuid(),
            ToStoreId = Guid.NewGuid(),
            Qty = 5,
        };

        [Test]
        public async Task Handle_MissingIds_ReturnsError()
        {
            var response = await _handler.Handle(new TransferStockRequestModel { Qty = 5 }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("required"));
        }

        [Test]
        public async Task Handle_SameFromAndToStore_ReturnsError()
        {
            var storeId = Guid.NewGuid();
            var request = ValidRequest();
            request.FromStoreId = storeId;
            request.ToStoreId = storeId;

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("cannot be the same"));
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
        public async Task Handle_BothMovementsSucceed_ReturnsSuccess()
        {
            var request = ValidRequest();

            _mediatorMock.Setup(m => m.Send(It.Is<RecordInventoryMovementRequestModel>(r => r.MovementType == "ISSUE"), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RecordInventoryMovementResponseModel { Success = true });
            _mediatorMock.Setup(m => m.Send(It.Is<RecordInventoryMovementRequestModel>(r => r.MovementType == "RECEIVE"), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RecordInventoryMovementResponseModel { Success = true });

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.True);
        }

        [Test]
        public async Task Handle_IssueFails_ReturnsErrorAndNeverAttemptsReceive()
        {
            var request = ValidRequest();

            _mediatorMock.Setup(m => m.Send(It.Is<RecordInventoryMovementRequestModel>(r => r.MovementType == "ISSUE"), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RecordInventoryMovementResponseModel { Success = false, Message = "Insufficient stock." });

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("Insufficient stock"));
            _mediatorMock.Verify(m => m.Send(It.Is<RecordInventoryMovementRequestModel>(r => r.MovementType == "RECEIVE"), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task Handle_ReceiveFails_ReturnsErrorWithoutOldContactAdminWording()
        {
            var request = ValidRequest();

            _mediatorMock.Setup(m => m.Send(It.Is<RecordInventoryMovementRequestModel>(r => r.MovementType == "ISSUE"), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RecordInventoryMovementResponseModel { Success = true });
            _mediatorMock.Setup(m => m.Send(It.Is<RecordInventoryMovementRequestModel>(r => r.MovementType == "RECEIVE"), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RecordInventoryMovementResponseModel { Success = false, Message = "Destination store not found." });

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("Destination store not found"));
            Assert.That(response.Message, Does.Contain("rolled back"));
            Assert.That(response.Message, Does.Not.Contain("Contact admin"));
        }
    }
}
