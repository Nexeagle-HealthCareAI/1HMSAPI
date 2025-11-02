using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Domain.Context;
using NUnit.Framework;
using System;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class DoctorShiftConfigHandlerTests
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
            var handler = new DoctorShiftConfigHandler(_context);
            Assert.That(handler, Is.Not.Null);
        }

        //[Test]
        //public void Handle_ShouldReturnShiftConfig_WhenValidDoctorId()
        //{
        //    // Arrange
        //    var doctorId = Guid.NewGuid();
        //    var handler = new DoctorShiftConfigHandler(_context);

        //    // Act
        //    var shiftConfig = handler.Handle(new DoctorShiftConfigQuery { DoctorId = doctorId });

        //    // Assert
        //    Assert.That(shiftConfig, Is.Not.Null, "Shift configuration should be returned.");
        //}

        //[Test]
        //public void Handle_ShouldThrowException_WhenInvalidDoctorId()
        //{
        //    // Arrange
        //    var handler = new DoctorShiftConfigHandler(_context);

        //    // Act & Assert
        //    Assert.Throws<Exception>(() => handler.Handle(new DoctorShiftConfigQuery()),
        //        "Expected exception when doctor ID is invalid.");
        //}
    }
}
