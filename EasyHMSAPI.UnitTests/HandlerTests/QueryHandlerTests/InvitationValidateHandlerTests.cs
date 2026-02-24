using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class InvitationValidateHandlerTests
    {
        private AppDbContext _context = null!;
        private InvitationValidateHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new InvitationValidateHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ValidToken_ReturnsSuccess()
        {
            // Arrange
            var token = "valid-token";
            var tokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            var roleId = Guid.NewGuid();
             _context.Roles.Add(new Role { RoleID = roleId, RoleName = "Test Role" });

            var invite = new UserInvitation
            {
                InvitationID = Guid.NewGuid(),
                HospitalID = Guid.NewGuid(),
                InvitedByUserID = Guid.NewGuid(),
                TokenHash = tokenHash,
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                Status = "Pending",
                RecipientName = "Test",
                RecipientMobile = "1234567890",
                RoleID = roleId
            };
            _context.UserInvitations.Add(invite);
            await _context.SaveChangesAsync();

            var request = new InvitationValidateRequestModel { Token = token };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.Name, Is.EqualTo("Test"));
        }

        [Test]
        public async Task Handle_InvalidToken_ReturnsFailure()
        {
            // Arrange
            var request = new InvitationValidateRequestModel { Token = "invalid" };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Invalid token."));
        }
    }
}
