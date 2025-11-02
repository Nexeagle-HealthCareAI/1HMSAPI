using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Domain.Context;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class DoctorBookedSlotsHandlerTests
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
            var handler = new DoctorBookedSlotsHandler(_context);
            Assert.That(handler, Is.Not.Null);
        }

        //[Test]
        //public void Handle_ShouldReturnBookedSlots_WhenValidDoctorId()
        //{
        //    // Arrange
        //    var doctorId = Guid.NewGuid();
        //    var handler = new DoctorBookedSlotsHandler(_context);

        //    // Act
        //    var bookedSlots = handler.Handle(new DoctorBookedSlotsQuery { DoctorId = doctorId });

        //    // Assert
        //    Assert.That(bookedSlots, Is.Not.Empty, "Booked slots should be returned.");
        //}

        //[Test]
        //public void Handle_ShouldThrowException_WhenInvalidDoctorId()
        //{
        //    // Arrange
        //    var handler = new DoctorBookedSlotsHandler(_context);

        //    // Act & Assert
        //    Assert.Throws<Exception>(() => handler.Handle(new DoctorBookedSlotsQuery()),
        //        "Expected exception when doctor ID is invalid.");
        //}
    }
}
