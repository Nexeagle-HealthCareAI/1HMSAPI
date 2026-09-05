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
    [TestFixture]
    public class AcceptThresholdSuggestionHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<IMediator> _mediatorMock = null!;
        private AcceptThresholdSuggestionHandler _handler = null!;
        private Guid _hospitalId;
        private Guid _itemId;
        private Guid _storeId;

        [SetUp]
        public async Task SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _mediatorMock = new Mock<IMediator>();
            _mediatorMock
                .Setup(m => m.Send(It.IsAny<CreateIndentRequestModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CreateIndentResponseModel { Success = true, IndentId = Guid.NewGuid(), IndentNumber = "IND-2026-000001" });
            _handler = new AcceptThresholdSuggestionHandler(_context, _mediatorMock.Object);
            _hospitalId = Guid.NewGuid();
            _itemId = Guid.NewGuid();
            _storeId = Guid.NewGuid();

            _context.InventoryItem.Add(new InventoryItem
            {
                InventoryItemId = _itemId,
                HospitalId = _hospitalId,
                ItemCode = "PARA",
                ItemName = "Paracetamol",
                Category = "DRUG",
                Unit = "TAB",
                CurrentStock = 5,
                MinStockLevel = 2,
                MaxStockLevel = 10,
                ReorderQty = 0,
                IsLasa = false,
                IsHighAlert = false,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();
        }

        [TearDown]
        public void TearDown()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        [Test]
        public async Task Handle_ValidRequest_UpdatesThresholds()
        {
            var response = await _handler.Handle(new AcceptThresholdSuggestionRequestModel
            {
                HospitalId = _hospitalId,
                InventoryItemId = _itemId,
                MinStockLevel = 10.5m,
                MaxStockLevel = 31.5m,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            var item = _context.InventoryItem.Single();
            Assert.That(item.MinStockLevel, Is.EqualTo(10.5m));
            Assert.That(item.MaxStockLevel, Is.EqualTo(31.5m));
        }

        [Test]
        public async Task Handle_MaxBelowMin_ReturnsError()
        {
            var response = await _handler.Handle(new AcceptThresholdSuggestionRequestModel
            {
                HospitalId = _hospitalId,
                InventoryItemId = _itemId,
                MinStockLevel = 20,
                MaxStockLevel = 10,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
        }

        [Test]
        public async Task Handle_ItemNotFound_ReturnsError()
        {
            var response = await _handler.Handle(new AcceptThresholdSuggestionRequestModel
            {
                HospitalId = _hospitalId,
                InventoryItemId = Guid.NewGuid(),
                MinStockLevel = 1,
                MaxStockLevel = 5,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
        }

        [Test]
        public async Task Handle_RequestingStoreIdSuppliedAndStockBelowMax_RaisesIndent()
        {
            // Item's CurrentStock is 5 (seeded in SetUp) -- MaxStockLevel of 20 leaves a real gap,
            // so this must raise a real Indent instead of being a dead-end threshold-only update.
            var response = await _handler.Handle(new AcceptThresholdSuggestionRequestModel
            {
                HospitalId = _hospitalId,
                InventoryItemId = _itemId,
                MinStockLevel = 10,
                MaxStockLevel = 20,
                RequestingStoreId = _storeId,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            Assert.That(response.IndentNumber, Is.EqualTo("IND-2026-000001"));
            _mediatorMock.Verify(m => m.Send(
                It.Is<CreateIndentRequestModel>(r =>
                    r.RequestingStoreId == _storeId &&
                    r.IsSystemGenerated == true &&
                    r.Lines.Count == 1 &&
                    r.Lines[0].InventoryItemId == _itemId &&
                    r.Lines[0].Qty == 15), // 20 (new max) - 5 (current stock)
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task Handle_RequestingStoreIdSuppliedButStockAlreadyAtMax_DoesNotRaiseIndent()
        {
            var response = await _handler.Handle(new AcceptThresholdSuggestionRequestModel
            {
                HospitalId = _hospitalId,
                InventoryItemId = _itemId,
                MinStockLevel = 2,
                MaxStockLevel = 5, // equals CurrentStock -- nothing to request
                RequestingStoreId = _storeId,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            Assert.That(response.IndentId, Is.Null);
            _mediatorMock.Verify(m => m.Send(It.IsAny<CreateIndentRequestModel>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task Handle_NoRequestingStoreId_OnlyUpdatesThresholdsAndNeverCallsIndent()
        {
            var response = await _handler.Handle(new AcceptThresholdSuggestionRequestModel
            {
                HospitalId = _hospitalId,
                InventoryItemId = _itemId,
                MinStockLevel = 10.5m,
                MaxStockLevel = 31.5m,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            _mediatorMock.Verify(m => m.Send(It.IsAny<CreateIndentRequestModel>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
