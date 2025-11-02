using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Domain.Context;
using NUnit.Framework;
using System;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class InvitationUpdateHandlerTests
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

        //[Test, Ignore("TODO: Implement test logic")]
        //public void Constructor_Smoke()
        //{
        //    var handler = new InvitationUpdateHandler(_context);
        //    Assert.That(handler, Is.Not.Null);
        //}

        //[Test]
        //public void Handle_ShouldUpdateInvitation_WhenValidInput()
        //{
        //    // Arrange
        //    var invitationId = Guid.NewGuid();
        //    var handler = new InvitationUpdateHandler(_context);

        //    // Act
        //    var result = handler.Handle(new InvitationUpdateCommand { InvitationId = invitationId });

        //    // Assert
        //    Assert.That(result, Is.True, "Invitation should be updated successfully.");
        //}

        //[Test]
        //public void Handle_ShouldThrowException_WhenInvalidInput()
        //{
        //    // Arrange
        //    var handler = new InvitationUpdateHandler(_context);

        //    // Act & Assert
        //    Assert.Throws<Exception>(() => handler.Handle(new InvitationUpdateCommand()),
        //        "Expected exception when input is invalid.");
        //}
    }
}
