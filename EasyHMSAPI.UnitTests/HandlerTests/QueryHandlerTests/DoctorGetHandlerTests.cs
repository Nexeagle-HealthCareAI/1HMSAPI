using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Domain.Context;
using NUnit.Framework;
using System;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class DoctorGetHandlerTests
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
            var handler = new DoctorGetHandler(_context);
            Assert.That(handler, Is.Not.Null);
        }

        //[Test]
        //public void Handle_ShouldReturnDoctorDetails_WhenValidDoctorId()
        //{
        //    // Arrange
        //    var doctorId = Guid.NewGuid();
        //    var handler = new DoctorGetHandler(_context);

        //    // Act
        //    var doctorDetails = handler.Handle(new DoctorGetQuery { DoctorId = doctorId });

        //    // Assert
        //    Assert.That(doctorDetails, Is.Not.Null, "Doctor details should be returned.");
        //}

        //[Test]
        //public void Handle_ShouldThrowException_WhenInvalidDoctorId()
        //{
        //    // Arrange
        //    var handler = new DoctorGetHandler(_context);

        //    // Act & Assert
        //    Assert.Throws<Exception>(() => handler.Handle(new DoctorGetQuery()),
        //        "Expected exception when doctor ID is invalid.");
        //}
    }
}
