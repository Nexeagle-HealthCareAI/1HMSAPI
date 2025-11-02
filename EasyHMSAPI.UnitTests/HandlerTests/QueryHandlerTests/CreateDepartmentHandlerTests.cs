using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Domain.Context;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class CreateDepartmentHandlerTests
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
            var handler = new CreateDepartmentHandler(_context);
            Assert.That(handler, Is.Not.Null);
        }

        //[Test]
        //public void Handle_ShouldCreateDepartment_WhenValidInput()
        //{
        //    // Arrange
        //    var departmentId = Guid.NewGuid();
        //    var handler = new CreateDepartmentHandler(_context);

        //    // Act
        //    var result = handler.Handle(new CreateDepartmentQuery { DepartmentId = departmentId });

        //    // Assert
        //    Assert.That(result, Is.Not.Null, "Department should be created successfully.");
        //}

        //[Test]
        //public void Handle_ShouldThrowException_WhenInvalidInput()
        //{
        //    // Arrange
        //    var handler = new CreateDepartmentHandler(_context);

        //    // Act & Assert
        //    Assert.Throws<Exception>(() => handler.Handle(new CreateDepartmentQuery()),
        //        "Expected exception when input is invalid.");
        //}
    }
}
