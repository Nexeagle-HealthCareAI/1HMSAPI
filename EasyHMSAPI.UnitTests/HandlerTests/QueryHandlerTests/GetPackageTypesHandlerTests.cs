using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class GetPackageTypesHandlerTests
    {
        private AppDbContext _context = null!;
        private GetPackageTypesHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetPackageTypesHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ReturnsActivePackageTypes_WithDeserializedComponents()
        {
            var hospitalId = Guid.NewGuid();
            _context.PackageTypes.Add(new PackageType
            {
                PackageTypeId = Guid.NewGuid(), HospitalId = hospitalId,
                Name = "Full Package", Price = 50000m,
                ComponentsJson = "[\"OT Med\",\"Ward Med\",\"Room Rent\",\"Procedure\"]",
                IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            _context.PackageTypes.Add(new PackageType
            {
                PackageTypeId = Guid.NewGuid(), HospitalId = hospitalId,
                Name = "Inactive Package", IsActive = false,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetPackageTypesRequestModel { HospitalId = hospitalId }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.PackageTypes, Has.Count.EqualTo(1)); // inactive excluded by default
            var full = response.PackageTypes.First();
            Assert.That(full.Name, Is.EqualTo("Full Package"));
            Assert.That(full.Components, Has.Count.EqualTo(4));
            Assert.That(full.Components, Contains.Item("Room Rent"));
        }

        [Test]
        public async Task Handle_NoComponentsJson_ReturnsEmptyList()
        {
            var hospitalId = Guid.NewGuid();
            _context.PackageTypes.Add(new PackageType
            {
                PackageTypeId = Guid.NewGuid(), HospitalId = hospitalId,
                Name = "Non Package", IsActive = true,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetPackageTypesRequestModel { HospitalId = hospitalId }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.PackageTypes[0].Components, Is.Empty);
            Assert.That(response.PackageTypes[0].Price, Is.Null);
        }

        [Test]
        public async Task Handle_IncludeInactive_ReturnsAll()
        {
            var hospitalId = Guid.NewGuid();
            _context.PackageTypes.Add(new PackageType
            {
                PackageTypeId = Guid.NewGuid(), HospitalId = hospitalId,
                Name = "Inactive", IsActive = false,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetPackageTypesRequestModel { HospitalId = hospitalId, IncludeInactive = true }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.PackageTypes, Has.Count.EqualTo(1));
        }
    }
}
