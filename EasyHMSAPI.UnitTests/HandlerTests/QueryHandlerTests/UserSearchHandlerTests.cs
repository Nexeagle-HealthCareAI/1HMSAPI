using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Domain.Context;
using NUnit.Framework;
using System;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class UserSearchHandlerTests
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
            var handler = new UserSearchHandler(_context);
            Assert.That(handler, Is.Not.Null);
        }

        //[Test]
        //public void Handle_ShouldReturnResults_WhenValidSearchQuery()
        //{
        //    // Arrange
        //    var query = "valid query";
        //    var handler = new UserSearchHandler(_context);

        //    // Act
        //    var results = handler.Handle(new UserSearchHandler { Query = query });

        //    // Assert
        //    Assert.That(results, Is.Not.Empty, "Search should return results.");
        //}

        //[Test]
        //public void Handle_ShouldThrowException_WhenInvalidSearchQuery()
        //{
        //    // Arrange
        //    var handler = new UserSearchHandler(_context);

        //    // Act & Assert
        //    Assert.Throws<Exception>(() => handler.Handle(new UserSearchHandler()),
        //        "Expected exception when search query is invalid.");
        //}
    }
}
