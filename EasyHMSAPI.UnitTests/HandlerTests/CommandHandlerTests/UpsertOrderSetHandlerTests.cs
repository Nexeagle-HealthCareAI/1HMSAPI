using System;
using System.Collections.Generic;
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
    public class UpsertOrderSetHandlerTests
    {
        private AppDbContext _context = null!;
        private UpsertOrderSetHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new UpsertOrderSetHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        private static List<OrderSetLineInput> ValidLines() => new()
        {
            new OrderSetLineInput { ItemName = "Paracetamol", OrderType = "medication", Dose = "500mg", Frequency = "TDS" },
            new OrderSetLineInput { ItemName = "CBC", OrderType = "lab" },
        };

        [Test]
        public async Task Handle_ValidRequest_CreatesOrderSet_WithSerializedLines()
        {
            var request = new UpsertOrderSetRequestModel
            {
                HospitalId = Guid.NewGuid(),
                Name = "Standard Post-Op Protocol",
                Lines = ValidLines(),
                LoggedInUserName = "Admin",
            };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            Assert.That(response.OrderSetId, Is.Not.Null);

            var saved = await _context.OrderSets.FindAsync(response.OrderSetId);
            Assert.That(saved, Is.Not.Null);
            Assert.That(saved!.Name, Is.EqualTo("Standard Post-Op Protocol"));
            Assert.That(saved.Category, Is.EqualTo("POST_OP"));
            Assert.That(saved.TemplateLinesJson, Does.Contain("Paracetamol"));
            Assert.That(saved.TemplateLinesJson, Does.Contain("MEDICATION"));
            Assert.That(saved.TemplateLinesJson, Does.Contain("LAB"));
        }

        [Test]
        public async Task Handle_MissingName_ReturnsError()
        {
            var request = new UpsertOrderSetRequestModel { HospitalId = Guid.NewGuid(), Lines = ValidLines() };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("Name"));
        }

        [Test]
        public async Task Handle_NoLines_ReturnsError()
        {
            var request = new UpsertOrderSetRequestModel { HospitalId = Guid.NewGuid(), Name = "Empty Set", Lines = new() };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("line"));
        }

        [Test]
        public async Task Handle_InvalidOrderTypeOnLine_ReturnsError()
        {
            var request = new UpsertOrderSetRequestModel
            {
                HospitalId = Guid.NewGuid(),
                Name = "Bad Set",
                Lines = new() { new OrderSetLineInput { ItemName = "X", OrderType = "SURGERY" } },
            };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("order type"));
        }

        [Test]
        public async Task Handle_ExistingOrderSetId_UpdatesInPlace()
        {
            var hospitalId = Guid.NewGuid();
            var existing = new OrderSet
            {
                OrderSetId = Guid.NewGuid(), HospitalId = hospitalId,
                Name = "Old Name", Category = "POST_OP", IsActive = true,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            _context.OrderSets.Add(existing);
            await _context.SaveChangesAsync();

            var request = new UpsertOrderSetRequestModel
            {
                OrderSetId = existing.OrderSetId,
                HospitalId = hospitalId,
                Name = "New Name",
                Lines = ValidLines(),
                IsActive = false,
            };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            var updated = await _context.OrderSets.FindAsync(existing.OrderSetId);
            Assert.That(updated!.Name, Is.EqualTo("New Name"));
            Assert.That(updated.IsActive, Is.False);
        }
    }
}
