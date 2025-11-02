using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Domain.Context;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class DoctorSlotsHandlerTests
    {
        private AppDbContext _context = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
        }

        [TearDown]
        public void TearDown()
        {
            _context?.Dispose();
            InMemoryDbContextFactory.Destroy(_context);
        }

        [Test, Ignore("TODO: Implement test logic")]
        public void Constructor_Smoke()
        {
            var handler = new DoctorSlotsHandler(_context);
            Assert.That(handler, Is.Not.Null);
        }

        //[Test]
        //public void Handle_ShouldReturnSlots_WhenValidDoctorId()
        //{
        //    // Arrange
        //    var doctorId = Guid.NewGuid();
        //    var handler = new DoctorSlotsHandler(_context);

        //    // Act
        //    var slots = handler.Handle(new DoctorSlotsQuery { DoctorId = doctorId });

        //    // Assert
        //    Assert.That(slots, Is.Not.Empty, "Slots should be returned.");
        //}

        //[Test]
        //public void Handle_ShouldThrowException_WhenInvalidDoctorId()
        //{
        //    // Arrange
        //    var handler = new DoctorSlotsHandler(_context);

        //    // Act & Assert
        //    Assert.Throws<Exception>(() => handler.Handle(new DoctorSlotsQuery()),
        //        "Expected exception when doctor ID is invalid.");
        //}
    }
}
