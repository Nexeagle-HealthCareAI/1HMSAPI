using System;
using System.Collections.Generic;
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
    public class CreateVendorReturnHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<IMediator> _mediatorMock = null!;
        private CreateVendorReturnHandler _handler = null!;
        private Guid _hospitalId;
        private Guid _vendorId;
        private Guid _itemId;
        private Guid _batchId;

        [SetUp]
        public async Task SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _mediatorMock = new Mock<IMediator>();
            _handler = new CreateVendorReturnHandler(_context, _mediatorMock.Object);

            _hospitalId = Guid.NewGuid();
            _vendorId = Guid.NewGuid();
            _itemId = Guid.NewGuid();
            _batchId = Guid.NewGuid();

            _context.Vendor.Add(new Vendor
            {
                VendorId = _vendorId,
                HospitalId = _hospitalId,
                VendorCode = "V1",
                VendorName = "Acme Pharma",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });

            _context.Batch.Add(new Batch
            {
                BatchId = _batchId,
                HospitalId = _hospitalId,
                InventoryItemId = _itemId,
                StoreId = Guid.NewGuid(),
                VendorId = _vendorId,
                BatchNumber = "NEAR-EXP",
                ExpiryDate = DateTime.UtcNow.AddDays(10),
                RemainingQty = 8,
                UnitCost = 15,
                Status = "ACTIVE",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });

            await _context.SaveChangesAsync();

            _mediatorMock
                .Setup(m => m.Send(It.IsAny<RecordInventoryMovementRequestModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RecordInventoryMovementResponseModel { Success = true });
        }

        [TearDown]
        public void TearDown()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        private CreateVendorReturnRequestModel ValidRequest() => new()
        {
            HospitalId = _hospitalId,
            VendorId = _vendorId,
            LoggedInUserName = "tester",
            Lines = new List<VendorReturnLineInput> { new() { BatchId = _batchId, Qty = 5 } },
        };

        [Test]
        public async Task Handle_MissingVendorId_ReturnsError()
        {
            var response = await _handler.Handle(new CreateVendorReturnRequestModel { HospitalId = _hospitalId }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("required"));
        }

        [Test]
        public async Task Handle_VendorNotFound_ReturnsError()
        {
            var request = ValidRequest();
            request.VendorId = Guid.NewGuid();

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("Vendor not found"));
        }

        [Test]
        public async Task Handle_QtyExceedsRemaining_ReturnsError()
        {
            var request = ValidRequest();
            request.Lines[0].Qty = 999;

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("remaining"));
            _mediatorMock.Verify(m => m.Send(It.IsAny<RecordInventoryMovementRequestModel>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task Handle_ValidReturn_DeductsStockViaAdjustOutWithVendorReturnContext()
        {
            var response = await _handler.Handle(ValidRequest(), CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.TotalQty, Is.EqualTo(5));
            Assert.That(response.TotalValue, Is.EqualTo(75));
            Assert.That(response.ReturnNoteNo, Is.Not.Null.And.Not.Empty);

            _mediatorMock.Verify(m => m.Send(
                It.Is<RecordInventoryMovementRequestModel>(r =>
                    r.MovementType == "ADJUST_OUT" && r.Qty == 5 && r.BatchId == _batchId && r.IsVendorReturnContext),
                It.IsAny<CancellationToken>()), Times.Once);

            var note = _context.VendorReturnNote.Single();
            Assert.That(note.TotalValue, Is.EqualTo(75));
            var line = _context.VendorReturnLine.Single();
            Assert.That(line.Qty, Is.EqualTo(5));
            Assert.That(line.UnitCost, Is.EqualTo(15));
        }

        [Test]
        public async Task Handle_StockDeductionFails_RollsBackAndReturnsError()
        {
            _mediatorMock
                .Setup(m => m.Send(It.IsAny<RecordInventoryMovementRequestModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RecordInventoryMovementResponseModel { Success = false, Message = "boom" });

            var response = await _handler.Handle(ValidRequest(), CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("boom"));
            Assert.That(_context.VendorReturnNote.Any(), Is.False);
        }
    }
}
