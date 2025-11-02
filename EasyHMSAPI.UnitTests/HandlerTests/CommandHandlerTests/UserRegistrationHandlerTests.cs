using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using Moq;
using NUnit.Framework;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class UserRegistrationHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<IJwtAuthService> _jwtMock = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _jwtMock = new Mock<IJwtAuthService>(MockBehavior.Strict);
        }

        [TearDown]
        public void TearDown()
        {

            _context?.Dispose();
            InMemoryDbContextFactory.Destroy(_context);
        }

        private void SeedRole(string roleName)
        {
            _context.Roles.Add(new Role
            {
                RoleID = Guid.NewGuid(),
                RoleName = roleName
            });
            _context.SaveChanges();
        }

        [Test]
        public async Task Handle_WhenMissingMobileOrRoles_ReturnsFailure()
        {
            // Arrange
            var handler = new UserRegistrationHandler(_context, new Mock<Microsoft.Extensions.Configuration.IConfiguration>().Object, _jwtMock.Object);

            // Act
            var resp1 = await handler.Handle(new UserRegistrationRequestModel { MobileNumber = null, Roles = "Admin" }, CancellationToken.None);
            var resp2 = await handler.Handle(new UserRegistrationRequestModel { MobileNumber = "999", Roles = null }, CancellationToken.None);

            // Assert
            Assert.That(resp1.Success, Is.False);
            Assert.That(resp2.Success, Is.False);
        }

        [Test]
        public async Task Handle_WhenInvalidRole_ReturnsFailure()
        {
            // Arrange
            SeedRole("User");
            _jwtMock.Setup(j => j.GenerateJwtToken(It.IsAny<System.Collections.Generic.List<Claim>>()))
                    .Returns("token");
            var handler = new UserRegistrationHandler(_context, new Mock<Microsoft.Extensions.Configuration.IConfiguration>().Object, _jwtMock.Object);

            // Act
            var resp = await handler.Handle(new UserRegistrationRequestModel { MobileNumber = "9990001111", Roles = "Unknown", FullName = "John" }, CancellationToken.None);

            // Assert
            Assert.That(resp.Success, Is.False);
            Assert.That(resp.Message, Does.Contain("Invalid role"));
        }

        [Test]
        public async Task Handle_WhenDuplicateMobile_ReturnsFailure()
        {
            // Arrange
            SeedRole("Admin");
            _context.Users.Add(new User { UserID = Guid.NewGuid(), MobileNumber = "9990001111", CreatedAt = DateTime.UtcNow });
            _context.SaveChanges();

            _jwtMock.Setup(j => j.GenerateJwtToken(It.IsAny<System.Collections.Generic.List<Claim>>()))
                    .Returns("token");
            var handler = new UserRegistrationHandler(_context, new Mock<Microsoft.Extensions.Configuration.IConfiguration>().Object, _jwtMock.Object);

            // Act
            var resp = await handler.Handle(new UserRegistrationRequestModel { MobileNumber = "9990001111", Roles = "Admin", FullName = "John" }, CancellationToken.None);

            // Assert
            Assert.That(resp.Success, Is.False);
            Assert.That(resp.Message, Does.Contain("already exists"));
        }

        [Test]
        public async Task Handle_WithValidInput_CreatesUserAndReturnsToken()
        {
            // Arrange
            const string roleName = "Admin";
            const string mobile = "9990001111";
            SeedRole(roleName);

            _jwtMock.Setup(j => j.GenerateJwtToken(It.IsAny<System.Collections.Generic.List<Claim>>()))
                    .Returns("test-token");

            var handler = new UserRegistrationHandler(_context, new Mock<Microsoft.Extensions.Configuration.IConfiguration>().Object, _jwtMock.Object);
            var request = new UserRegistrationRequestModel
            {
                MobileNumber = mobile,
                Roles = roleName,
                FullName = "John Doe"
            };

            // Act
            var response = await handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.AccessToken, Is.EqualTo("test-token"));
            Assert.That(response.UserId, Is.Not.EqualTo(Guid.Empty));

            // Verify entities created
            Assert.That(_context.Users.Any(u => u.MobileNumber == mobile), Is.True);
            var user = _context.Users.First(u => u.MobileNumber == mobile);
            Assert.That(_context.UserAuths.Any(ua => ua.UserID == user.UserID), Is.True);
            Assert.That(_context.UserRoles.Any(ur => ur.UserID == user.UserID), Is.True);
            Assert.That(_context.UserProfiles.Any(up => up.UserID == user.UserID && !string.IsNullOrWhiteSpace(up.EmployeeID)), Is.True);

            _jwtMock.Verify(j => j.GenerateJwtToken(It.IsAny<System.Collections.Generic.List<Claim>>()), Times.Once);
        }
    }
}
