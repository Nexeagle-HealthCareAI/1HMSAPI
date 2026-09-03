using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class AcceptThresholdSuggestionHandlerTests
    {
        private AppDbContext _context = null!;
        private AcceptThresholdSuggestionHandler _handler = null!;
        private Guid _hospitalId;
        private Guid _itemId;

        [SetUp]
        public async Task SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new AcceptThresholdSuggestionHandler(_context);
            _hospitalId = Guid.NewGuid();
            _itemId = Guid.NewGuid();

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
    }
}
