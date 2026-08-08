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
    public class GetOrderSetsHandlerTests
    {
        private AppDbContext _context = null!;
        private GetOrderSetsHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetOrderSetsHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ReturnsActiveOrderSets_WithDeserializedLines()
        {
            var hospitalId = Guid.NewGuid();
            _context.OrderSets.Add(new OrderSet
            {
                OrderSetId = Guid.NewGuid(), HospitalId = hospitalId,
                Name = "Standard Post-Op Protocol", Category = "POST_OP",
                TemplateLinesJson = "[{\"ItemName\":\"Paracetamol\",\"OrderType\":\"MEDICATION\",\"Dose\":\"500mg\"},{\"ItemName\":\"CBC\",\"OrderType\":\"LAB\"}]",
                IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            _context.OrderSets.Add(new OrderSet
            {
                OrderSetId = Guid.NewGuid(), HospitalId = hospitalId,
                Name = "Inactive Set", Category = "POST_OP", IsActive = false,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetOrderSetsRequestModel { HospitalId = hospitalId }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.OrderSets, Has.Count.EqualTo(1)); // inactive excluded by default
            var set = response.OrderSets.First();
            Assert.That(set.Name, Is.EqualTo("Standard Post-Op Protocol"));
            Assert.That(set.Lines, Has.Count.EqualTo(2));
            Assert.That(set.Lines.Select(l => l.OrderType), Is.EquivalentTo(new[] { "MEDICATION", "LAB" }));
        }

        [Test]
        public async Task Handle_NoTemplateLinesJson_ReturnsEmptyLines()
        {
            var hospitalId = Guid.NewGuid();
            _context.OrderSets.Add(new OrderSet
            {
                OrderSetId = Guid.NewGuid(), HospitalId = hospitalId,
                Name = "Empty Set", Category = "POST_OP", IsActive = true,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetOrderSetsRequestModel { HospitalId = hospitalId }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.OrderSets[0].Lines, Is.Empty);
        }

        [Test]
        public async Task Handle_FilterByCategory_ExcludesOtherCategories()
        {
            var hospitalId = Guid.NewGuid();
            _context.OrderSets.Add(new OrderSet
            {
                OrderSetId = Guid.NewGuid(), HospitalId = hospitalId,
                Name = "Post-Op Set", Category = "POST_OP", IsActive = true,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            _context.OrderSets.Add(new OrderSet
            {
                OrderSetId = Guid.NewGuid(), HospitalId = hospitalId,
                Name = "Other Set", Category = "OTHER", IsActive = true,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetOrderSetsRequestModel { HospitalId = hospitalId, Category = "post_op" }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.OrderSets, Has.Count.EqualTo(1));
            Assert.That(response.OrderSets[0].Name, Is.EqualTo("Post-Op Set"));
        }

        [Test]
        public async Task Handle_IncludeInactive_ReturnsAll()
        {
            var hospitalId = Guid.NewGuid();
            _context.OrderSets.Add(new OrderSet
            {
                OrderSetId = Guid.NewGuid(), HospitalId = hospitalId,
                Name = "Inactive", Category = "POST_OP", IsActive = false,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetOrderSetsRequestModel { HospitalId = hospitalId, IncludeInactive = true }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.OrderSets, Has.Count.EqualTo(1));
        }
    }
}
