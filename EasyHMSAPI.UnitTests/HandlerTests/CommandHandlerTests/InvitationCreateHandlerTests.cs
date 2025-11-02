using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class InvitationCreateHandlerTests
    {
        //private AppDbContext _context = null!;
        //private Mock<IEmailService> _emailServiceMock = null!;
        //private Mock<ICryptoService> _cryptoServiceMock = null!;

        //[SetUp]
        //public void SetUp()
        //{
        //    _context = InMemoryDbContextFactory.CreateContext();
        //    _emailServiceMock = new Mock<IEmailService>();
        //    _cryptoServiceMock = new Mock<ICryptoService>();
        //}

        //[TearDown]
        //public void TearDown()
        //{
        //    InMemoryDbContextFactory.Destroy(_context);
        //}

        //[Test, Ignore("TODO: Implement test logic")]
        //public void Constructor_Smoke()
        //{
        //    var handler = new InvitationCreateHandler(_context, _emailServiceMock.Object, _cryptoServiceMock.Object);
        //    Assert.That(handler, Is.Not.Null);
        //}

        //[Test]
        //public void Handle_ShouldCreateInvitation_WhenValidInput()
        //{
        //    // Arrange
        //    var invitationId = Guid.NewGuid();
        //    var handler = new InvitationCreateHandler(_context, _emailServiceMock.Object, _cryptoServiceMock.Object);

        //    // Act
        //    var result = handler.Handle(new InvitationCreateCommand { InvitationId = invitationId });

        //    // Assert
        //    Assert.That(result, Is.True, "Invitation should be created successfully.");
        //}

        //[Test]
        //public void Handle_ShouldThrowException_WhenInvalidInput()
        //{
        //    // Arrange
        //    var handler = new InvitationCreateHandler(_context, _emailServiceMock.Object, _cryptoServiceMock.Object);

        //    // Act & Assert
        //    Assert.Throws<Exception>(() => handler.Handle(new InvitationCreateCommand()),
        //        "Expected exception when input is invalid.");
        //}
    }
}
