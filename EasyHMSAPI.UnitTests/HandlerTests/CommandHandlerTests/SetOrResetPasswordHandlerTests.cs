using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class SetOrResetPasswordHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<IMaskingService> _maskingServiceMock = null!;
        private SetOrResetPasswordHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _maskingServiceMock = new Mock<IMaskingService>();
            _maskingServiceMock.Setup(m => m.IsMaskingEnabled()).Returns(false);
            _maskingServiceMock.Setup(m => m.Mask(It.IsAny<string>())).Returns((string s) => s);
            _handler = new SetOrResetPasswordHandler(_context, _maskingServiceMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_SetPassword_UpdatesPasswordAndEmail()
        {
            // Arrange
            var user = TestDataFactory.SeedUser(_context, password: "oldPassword", email: "old@test.com");
            var userAuth = await _context.UserAuths.FirstOrDefaultAsync(u => u.UserID == user.UserID);
            await _context.SaveChangesAsync();

            var request = new SetOrResetPasswordRequestModel
            {
                UserId = user.UserID,
                Scope = "set-password",
                Email = "new@test.com",
                Password = "newPassword"
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.Message, Does.Contain("successfully updated"));

            var updatedUser = await _context.Users.FindAsync(user.UserID);
            Assert.That(updatedUser!.Email, Is.EqualTo("new@test.com"));

            var updatedAuth = await _context.UserAuths.FindAsync(userAuth!.UserAuthID);
            var expectedHash = BitConverter.ToString(SHA256.HashData(Encoding.UTF8.GetBytes("newPassword"))).Replace("-", "").ToLower();
            Assert.That(updatedAuth!.HashedPassword, Is.EqualTo(expectedHash));
        }

        [Test]
        public async Task Handle_ResetPassword_UpdatesPassword()
        {
            // Arrange
            var user = TestDataFactory.SeedUser(_context, password: "oldPassword");
             var userAuth = await _context.UserAuths.FirstOrDefaultAsync(u => u.UserID == user.UserID);
            await _context.SaveChangesAsync();

            var request = new SetOrResetPasswordRequestModel
            {
                UserId = user.UserID,
                Scope = "reset-password",
                Password = "newPassword"
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.Message, Does.Contain("successfully reset"));
            
            var updatedAuth = await _context.UserAuths.FindAsync(userAuth!.UserAuthID);
            var expectedHash = BitConverter.ToString(SHA256.HashData(Encoding.UTF8.GetBytes("newPassword"))).Replace("-", "").ToLower();
            Assert.That(updatedAuth!.HashedPassword, Is.EqualTo(expectedHash));
        }

        [Test]
        public async Task Handle_SamePassword_ReturnsFailure()
        {
             // Arrange
            var user = TestDataFactory.SeedUser(_context, password: "oldPassword");
            await _context.SaveChangesAsync();

            var request = new SetOrResetPasswordRequestModel
            {
                UserId = user.UserID,
                Scope = "reset-password",
                Password = "oldPassword" // Same password
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("cannot be same"));
        }
    }
}
