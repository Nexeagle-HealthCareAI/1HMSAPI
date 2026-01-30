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
    public class DoctorTimeOffDeleteHandlerTests
    {
        private AppDbContext _context = null!;
        private DoctorTimeOffDeleteHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new DoctorTimeOffDeleteHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ValidId_DeletesTimeOff()
        {
            // Arrange
            var timeOffId = Guid.NewGuid();
            var timeOff = new DoctorTimeOff { TimeOffID = timeOffId };
            _context.DoctorTimeOffs.Add(timeOff);
            await _context.SaveChangesAsync();

            var request = new DoctorTimeOffDeleteRequestModel { TimeOffId = timeOffId };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            var deleted = await _context.DoctorTimeOffs.FindAsync(timeOffId);
            Assert.That(deleted, Is.Null);
        }

        [Test]
        public async Task Handle_NotFound_ReturnsFailure()
        {
            // Arrange
            var request = new DoctorTimeOffDeleteRequestModel { TimeOffId = Guid.NewGuid() };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Time-off not found"));
        }
    }
}
