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
    public class DoctorOverrideDeleteHandlerTests
    {
        private AppDbContext _context = null!;
        private DoctorOverrideDeleteHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new DoctorOverrideDeleteHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ValidId_DeletesOverride()
        {
            // Arrange
            var overrideId = Guid.NewGuid();
            var shiftOverride = new DoctorShiftOverride { OverrideID = overrideId, ShiftName = "Morning" };
            _context.DoctorShiftOverrides.Add(shiftOverride);
            await _context.SaveChangesAsync();

            var request = new DoctorOverrideDeleteRequestModel { OverrideId = overrideId };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            var deleted = await _context.DoctorShiftOverrides.FindAsync(overrideId);
            Assert.That(deleted, Is.Null);
        }

        [Test]
        public async Task Handle_NotFound_ReturnsFailure()
        {
            // Arrange
            var request = new DoctorOverrideDeleteRequestModel { OverrideId = Guid.NewGuid() };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Override not found"));
        }
    }
}
