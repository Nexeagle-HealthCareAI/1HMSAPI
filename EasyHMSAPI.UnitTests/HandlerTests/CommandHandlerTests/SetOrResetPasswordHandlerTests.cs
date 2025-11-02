using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Domain.Context;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class SetOrResetPasswordHandlerTests
    {
        //private AppDbContext _context = null!;

        //[SetUp]
        //public void SetUp()
        //{
        //    _context = InMemoryDbContextFactory.CreateContext();
        //}

        //[TearDown]
        //public void TearDown()
        //{
        //    _context?.Dispose();
        //    InMemoryDbContextFactory.Destroy(_context);
        //}

        //[Test, Ignore("TODO: Implement test logic")]
        //public void Constructor_Smoke()
        //{
        //    var handler = new SetOrResetPasswordHandler(_context);
        //    Assert.That(handler, Is.Not.Null);
        //}

        //[Test]
        //public void Handle_ShouldSetOrResetPassword_WhenValidInput()
        //{
        //    // Arrange
        //    var userId = Guid.NewGuid();
        //    var handler = new SetOrResetPasswordHandler(_context);

        //    // Act
        //    var result = handler.Handle(new SetOrResetPasswordCommand { UserId = userId });

        //    // Assert
        //    Assert.That(result, Is.True, "Password should be set or reset successfully.");
        //}

        //[Test]
        //public void Handle_ShouldThrowException_WhenInvalidInput()
        //{
        //    // Arrange
        //    var handler = new SetOrResetPasswordHandler(_context);

        //    // Act & Assert
        //    Assert.Throws<Exception>(() => handler.Handle(new SetOrResetPasswordCommand()),
        //        "Expected exception when input is invalid.");
        //}
    }
}
