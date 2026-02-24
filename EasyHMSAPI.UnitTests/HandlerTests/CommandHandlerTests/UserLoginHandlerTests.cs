using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.Extensions.Configuration;
using Moq;
using NUnit.Framework;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class UserLoginHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<IJwtAuthService> _jwtAuthServiceMock = null!;
        private Mock<IConfiguration> _configurationMock = null!;
        private Mock<IMaskingService> _maskingServiceMock = null!;
        private UserLoginHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _jwtAuthServiceMock = new Mock<IJwtAuthService>();
            _configurationMock = new Mock<IConfiguration>();
            _maskingServiceMock = new Mock<IMaskingService>();
            _maskingServiceMock.Setup(m => m.IsMaskingEnabled()).Returns(false);
            _handler = new UserLoginHandler(_context, _jwtAuthServiceMock.Object, _configurationMock.Object, _maskingServiceMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ValidCredentials_ReturnsSuccessAndToken()
        {
            // Arrange
            var email = "valid@example.com";
            var password = "password123";
            TestDataFactory.SeedUser(_context, email: email, password: password);
            _jwtAuthServiceMock.Setup(x => x.GenerateJwtToken(It.IsAny<List<Claim>>())).Returns("valid_token");

            var request = new UserLoginRequestModel { EmailOrPhone = email, Password = password, IsLoginWithOtp = false };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.AccessToken, Is.EqualTo("valid_token"));
            Assert.That(response.Message, Is.EqualTo("Login Successful"));
        }

        [Test]
        public async Task Handle_InvalidPassword_ReturnsFailure()
        {
             // Arrange
            var email = "valid@example.com";
            TestDataFactory.SeedUser(_context, email: email, password: "password123");
            var request = new UserLoginRequestModel { EmailOrPhone = email, Password = "wrongpassword", IsLoginWithOtp = false };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Invalid Password"));
            Assert.That(response.AccessToken, Is.Null);
        }

        [Test]
        public async Task Handle_UserNotFound_ReturnsFailure()
        {
             // Arrange
            var request = new UserLoginRequestModel { EmailOrPhone = "nonexistent@example.com", Password = "password", IsLoginWithOtp = false };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

             // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Invalid Email or Mobile Number")); // Based on logic when user is null
        }

        [Test]
        public async Task Handle_InactiveUser_ReturnsFailure()
        {
             // Arrange
            var email = "inactive@example.com";
            TestDataFactory.SeedUser(_context, email: email, isActive: false);
            var request = new UserLoginRequestModel { EmailOrPhone = email, Password = "any", IsLoginWithOtp = false };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("User account is not active"));
        }
    }
}
