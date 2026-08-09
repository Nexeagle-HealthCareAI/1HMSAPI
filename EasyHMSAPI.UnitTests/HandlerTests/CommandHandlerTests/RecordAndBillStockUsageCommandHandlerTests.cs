using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class RecordAndBillStockUsageCommandHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<IMediator> _mediatorMock = null!;
        private RecordAndBillStockUsageCommandHandler _handler = null!;
        private Guid _hospitalId;
        private Guid _storeId;
        private Guid _encounterId;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _mediatorMock = new Mock<IMediator>();
            _handler = new RecordAndBillStockUsageCommandHandler(_mediatorMock.Object, _context, NullLogger<RecordAndBillStockUsageCommandHandler>.Instance);
            _hospitalId = Guid.NewGuid();
            _storeId = Guid.NewGuid();
            _encounterId = Guid.NewGuid();
        }

        [TearDown]
        public void TearDown()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        private InventoryItem SeedItem(Guid? chargeId)
        {
            var item = new InventoryItem
            {
                InventoryItemId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                ItemCode = "SYR-10",
                ItemName = "10ml Syringe",
                Category = "CONSUMABLE",
                Unit = "PCS",
                IsTaxable = false,
                CurrentStock = 100,
                MinStockLevel = 0,
                ReorderQty = 0,
                ChargeId = chargeId,
                DefaultRate = 15,
                IsLasa = false,
                IsHighAlert = false,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            _context.InventoryItem.Add(item);
            return item;
        }

        private RecordAndBillStockUsageRequestModel ValidRequest(Guid inventoryItemId) => new()
        {
            HospitalId = _hospitalId,
            StoreId = _storeId,
            InventoryItemId = inventoryItemId,
            Qty = 2,
            EncounterId = _encounterId,
            PatientId = "PT001",
        };

        [Test]
        public async Task Handle_MissingFields_ReturnsError()
        {
            var response = await _handler.Handle(new RecordAndBillStockUsageRequestModel { Qty = 1 }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("required"));
        }

        [Test]
        public async Task Handle_ItemNotFound_ReturnsError()
        {
            var response = await _handler.Handle(ValidRequest(Guid.NewGuid()), CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("not found"));
        }

        [Test]
        public async Task Handle_ItemHasNoChargeConfigured_RecordsMovementOnly_NoChargePosted()
        {
            var item = SeedItem(chargeId: null);
            await _context.SaveChangesAsync();

            _mediatorMock.Setup(m => m.Send(It.IsAny<RecordInventoryMovementRequestModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RecordInventoryMovementResponseModel { Success = true, InventoryMovementId = Guid.NewGuid() });

            var response = await _handler.Handle(ValidRequest(item.InventoryItemId), CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.NoChargeConfigured, Is.True);
            Assert.That(response.ChargeEventId, Is.Null);
            _mediatorMock.Verify(m => m.Send(It.IsAny<AddChargeEventRequestModel>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task Handle_ItemHasChargeConfigured_RecordsMovementAndPostsCharge()
        {
            var chargeId = Guid.NewGuid();
            var item = SeedItem(chargeId);
            await _context.SaveChangesAsync();

            var movementId = Guid.NewGuid();
            var chargeEventId = Guid.NewGuid();
            _mediatorMock.Setup(m => m.Send(It.IsAny<RecordInventoryMovementRequestModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RecordInventoryMovementResponseModel { Success = true, InventoryMovementId = movementId });
            _mediatorMock.Setup(m => m.Send(It.Is<AddChargeEventRequestModel>(r => r.Charges!.Count == 1 && r.Charges[0].ChargeId == chargeId), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AddChargeEventResponseModel
                {
                    Success = true,
                    Data = new AddChargesData { ChargeEvents = new() { new ChargeEventDetail { ChargeEventId = chargeEventId } } },
                });

            var response = await _handler.Handle(ValidRequest(item.InventoryItemId), CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.NoChargeConfigured, Is.False);
            Assert.That(response.InventoryMovementId, Is.EqualTo(movementId));
            Assert.That(response.ChargeEventId, Is.EqualTo(chargeEventId));
        }

        [Test]
        public async Task Handle_MovementFails_ReturnsErrorAndNeverAttemptsCharge()
        {
            var item = SeedItem(chargeId: Guid.NewGuid());
            await _context.SaveChangesAsync();

            _mediatorMock.Setup(m => m.Send(It.IsAny<RecordInventoryMovementRequestModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RecordInventoryMovementResponseModel { Success = false, Message = "Insufficient stock." });

            var response = await _handler.Handle(ValidRequest(item.InventoryItemId), CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("Insufficient stock"));
            _mediatorMock.Verify(m => m.Send(It.IsAny<AddChargeEventRequestModel>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task Handle_ChargePostingFails_ReturnsError()
        {
            var item = SeedItem(chargeId: Guid.NewGuid());
            await _context.SaveChangesAsync();

            _mediatorMock.Setup(m => m.Send(It.IsAny<RecordInventoryMovementRequestModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RecordInventoryMovementResponseModel { Success = true, InventoryMovementId = Guid.NewGuid() });
            _mediatorMock.Setup(m => m.Send(It.IsAny<AddChargeEventRequestModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AddChargeEventResponseModel { Success = false, Message = "Encounter not found." });

            var response = await _handler.Handle(ValidRequest(item.InventoryItemId), CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("Encounter not found"));
        }
    }
}
