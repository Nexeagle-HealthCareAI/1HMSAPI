using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.EntityFrameworkCore;
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
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        [Test]
        public async Task Handle_OverrideNotFound_ReturnsError()
        {
            // Arrange
            var request = new DoctorOverrideDeleteRequestModel
            {
                OverrideId = Guid.NewGuid()
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Override not found"));
        }

        [Test]
        public async Task Handle_Success_DeletesOverride()
        {
            // Arrange
            var overrideId = Guid.NewGuid();
            var doctorId = Guid.NewGuid();
            var hospitalId = Guid.NewGuid();
            var userId = Guid.NewGuid();

            _context.Users.Add(TestEntityFactory.CreateUser(userId));
            _context.Doctors.Add(TestEntityFactory.CreateDoctor(doctorId, userId));
            _context.DoctorShiftOverrides.Add(TestEntityFactory.CreateDoctorShiftOverride(overrideId, doctorId, hospitalId));
            await _context.SaveChangesAsync();

            var request = new DoctorOverrideDeleteRequestModel
            {
                OverrideId = overrideId
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.Message, Is.EqualTo("Override deleted"));

            var deletedOverride = await _context.DoctorShiftOverrides.FindAsync(overrideId);
            Assert.That(deletedOverride, Is.Null);
        }
    }
}
