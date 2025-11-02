using System;
using NUnit.Framework;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Application.Handlers.CommandHandlers;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class DeletePersonalizedDataHandlerTests
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
            var handler = new DeletePersonalizedDataHandler(_context);
            Assert.That(handler, Is.Not.Null);
        }

        //[Test]
        //public void Handle_ShouldDeletePersonalizedData_WhenDataExists()
        //{
        //    // Arrange
        //    var dataId = Guid.NewGuid();
        //    var handler = new DeletePersonalizedDataHandler(_context);

        //    // Act
        //    var result = handler.Handle(new DeletePersonalizedDataCommand { DataId = dataId });

        //    // Assert
        //    Assert.That(result, Is.True, "Personalized data should be deleted successfully.");
        //}

        //[Test]
        //public void Handle_ShouldThrowException_WhenDataDoesNotExist()
        //{
        //    // Arrange
        //    var dataId = Guid.NewGuid();
        //    var handler = new DeletePersonalizedDataHandler(_context);

        //    // Act & Assert
        //    Assert.Throws<Exception>(() => handler.Handle(new DeletePersonalizedDataCommand { DataId = dataId }),
        //        "Expected exception when personalized data does not exist.");
        //}
    }
}
