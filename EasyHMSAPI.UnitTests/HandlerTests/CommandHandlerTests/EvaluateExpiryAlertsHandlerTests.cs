using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class EvaluateExpiryAlertsHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<ISmsService> _smsMock = null!;
        private EvaluateExpiryAlertsHandler _handler = null!;
        private Guid _hospitalId;
        private Guid _storeId;
        private Guid _itemId;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _smsMock = new Mock<ISmsService>();
            _handler = new EvaluateExpiryAlertsHandler(_context, _smsMock.Object, NullLogger<EvaluateExpiryAlertsHandler>.Instance);
            _hospitalId = Guid.NewGuid();
            _storeId = Guid.NewGuid();
            _itemId = Guid.NewGuid();

            _context.Hospitals.Add(new Hospital
            {
                HospitalID = _hospitalId,
                Name = "Test Hospital",
                Type = "GENERAL",
                RegistrationNumber = "REG-1",
                Contact = "9999999999",
                Location = "Loc",
                City = "City",
                State = "State",
                Country = "IN",
                Pincode = "800001",
                CreatedByUserID = Guid.NewGuid(),
                IsActive = true,
            });

            _context.InventoryItem.Add(new InventoryItem
            {
                InventoryItemId = _itemId,
                HospitalId = _hospitalId,
                ItemCode = "PARA",
                ItemName = "Paracetamol",
                Category = "DRUG",
                Unit = "TAB",
                CurrentStock = 100,
                MinStockLevel = 0,
                ReorderQty = 0,
                IsLasa = false,
                IsHighAlert = false,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
        }

        [TearDown]
        public void TearDown()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        private void SeedBatch(int daysToExpiry, decimal remainingQty = 10)
        {
            _context.Batch.Add(new Batch
            {
                BatchId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                InventoryItemId = _itemId,
                StoreId = _storeId,
                BatchNumber = $"B-{Guid.NewGuid().ToString().Substring(0, 5)}",
                ExpiryDate = DateTime.UtcNow.Date.AddDays(daysToExpiry),
                ReceivedQty = remainingQty,
                RemainingQty = remainingQty,
                Status = "ACTIVE",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
        }

        [Test]
        public async Task Handle_NoNearExpiryBatches_ReturnsZeroAlerts()
        {
            SeedBatch(daysToExpiry: 200);
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new EvaluateExpiryAlertsRequestModel { HospitalId = _hospitalId }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.AlertsRaised, Is.EqualTo(0));
        }

        [Test]
        public async Task Handle_BatchWithin30Days_RaisesExpiry30Alert()
        {
            SeedBatch(daysToExpiry: 15);
            await _context.SaveChangesAsync();
            _smsMock.Setup(s => s.SendInvitationSmsAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

            var response = await _handler.Handle(new EvaluateExpiryAlertsRequestModel { HospitalId = _hospitalId }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.AlertsRaised, Is.EqualTo(1));
            Assert.That(response.SmsDispatched, Is.EqualTo(1));

            var alert = _context.Alert.Single();
            Assert.That(alert.AlertCode, Is.EqualTo("EXPIRY_30"));
            Assert.That(alert.Severity, Is.EqualTo("CRITICAL"));
        }

        [Test]
        public async Task Handle_SameBatchEvaluatedTwice_SkipsDuplicateAlert()
        {
            SeedBatch(daysToExpiry: 45);
            await _context.SaveChangesAsync();
            _smsMock.Setup(s => s.SendInvitationSmsAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

            await _handler.Handle(new EvaluateExpiryAlertsRequestModel { HospitalId = _hospitalId }, CancellationToken.None);
            var second = await _handler.Handle(new EvaluateExpiryAlertsRequestModel { HospitalId = _hospitalId }, CancellationToken.None);

            Assert.That(second.AlertsRaised, Is.EqualTo(0));
            Assert.That(second.AlertsSkippedDuplicate, Is.EqualTo(1));
            Assert.That(_context.Alert.Count(), Is.EqualTo(1));
        }

        [Test]
        public async Task Handle_ExhaustedBatch_IsIgnored()
        {
            SeedBatch(daysToExpiry: 10, remainingQty: 0);
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new EvaluateExpiryAlertsRequestModel { HospitalId = _hospitalId }, CancellationToken.None);

            Assert.That(response.BatchesScanned, Is.EqualTo(0));
            Assert.That(response.AlertsRaised, Is.EqualTo(0));
        }
    }
}
