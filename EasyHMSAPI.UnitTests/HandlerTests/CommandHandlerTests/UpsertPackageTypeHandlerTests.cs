using System;
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
    public class UpsertPackageTypeHandlerTests
    {
        private AppDbContext _context = null!;
        private UpsertPackageTypeHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new UpsertPackageTypeHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ValidRequest_CreatesPackageType_WithComponents()
        {
            var hospitalId = Guid.NewGuid();
            var request = new UpsertPackageTypeRequestModel
            {
                HospitalId = hospitalId,
                Name = "Full Package",
                Price = 50000m,
                Components = new() { "OT Med", "Ward Med", "Room Rent", "Procedure" },
                IsActive = true,
                LoggedInUserName = "Admin",
            };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.PackageTypeId, Is.Not.Null);

            var saved = await _context.PackageTypes.FindAsync(response.PackageTypeId);
            Assert.That(saved, Is.Not.Null);
            Assert.That(saved!.Name, Is.EqualTo("Full Package"));
            Assert.That(saved.Price, Is.EqualTo(50000m));
            Assert.That(saved.ComponentsJson, Does.Contain("OT Med"));
        }

        [Test]
        public async Task Handle_NoNameOrPriceOrComponents_StillSucceeds_AllFieldsOptional()
        {
            // Only Name is required; Price and Components must be fully optional.
            var request = new UpsertPackageTypeRequestModel
            {
                HospitalId = Guid.NewGuid(),
                Name = "Non Package",
            };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            var saved = await _context.PackageTypes.FindAsync(response.PackageTypeId);
            Assert.That(saved!.Price, Is.Null);
            Assert.That(saved.ComponentsJson, Is.Null);
        }

        [Test]
        public async Task Handle_BlankComponentEntries_AreDropped()
        {
            var request = new UpsertPackageTypeRequestModel
            {
                HospitalId = Guid.NewGuid(),
                Name = "Full Package",
                Components = new() { "OT Med", "  ", "", "Room Rent" },
            };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            var saved = await _context.PackageTypes.FindAsync(response.PackageTypeId);
            Assert.That(saved!.ComponentsJson, Does.Not.Contain("\"  \""));
            Assert.That(saved.ComponentsJson, Does.Contain("OT Med"));
            Assert.That(saved.ComponentsJson, Does.Contain("Room Rent"));
        }

        [Test]
        public async Task Handle_MissingName_ReturnsError()
        {
            var request = new UpsertPackageTypeRequestModel { HospitalId = Guid.NewGuid() };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("required"));
        }

        [Test]
        public async Task Handle_ExistingPackageTypeId_UpdatesInPlace()
        {
            var hospitalId = Guid.NewGuid();
            var existing = new PackageType
            {
                PackageTypeId = Guid.NewGuid(), HospitalId = hospitalId,
                Name = "Old Name", IsActive = true,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            _context.PackageTypes.Add(existing);
            await _context.SaveChangesAsync();

            var request = new UpsertPackageTypeRequestModel
            {
                PackageTypeId = existing.PackageTypeId,
                HospitalId = hospitalId,
                Name = "New Name",
                Price = 1000m,
                IsActive = false,
            };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            var updated = await _context.PackageTypes.FindAsync(existing.PackageTypeId);
            Assert.That(updated!.Name, Is.EqualTo("New Name"));
            Assert.That(updated.IsActive, Is.False);
        }
    }
}
