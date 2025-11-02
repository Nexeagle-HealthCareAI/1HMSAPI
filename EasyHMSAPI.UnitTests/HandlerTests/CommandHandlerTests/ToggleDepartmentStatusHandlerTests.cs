using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Domain.Context;
using NUnit.Framework;
using System;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class ToggleDepartmentStatusHandlerTests
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
            var handler = new ToggleDepartmentStatusHandler(_context);
            Assert.That(handler, Is.Not.Null);
        }

        //[Test]
        //public void Handle_ShouldToggleDepartmentStatus_WhenValidInput()
        //{
        //    // Arrange
        //    var departmentId = Guid.NewGuid();
        //    var handler = new ToggleDepartmentStatusHandler(_context);

        //    // Act
        //    var result = handler.Handle(new ToggleDepartmentStatusCommand { DepartmentId = departmentId });

        //    // Assert
        //    Assert.That(result, Is.True, "Department status should be toggled successfully.");
        //}

        //[Test]
        //public void Handle_ShouldThrowException_WhenInvalidInput()
        //{
        //    // Arrange
        //    var handler = new ToggleDepartmentStatusHandler(_context);

        //    // Act & Assert
        //    Assert.Throws<Exception>(() => handler.Handle(new ToggleDepartmentStatusCommand()),
        //        "Expected exception when input is invalid.");
        //}
    }
}
