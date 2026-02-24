using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Data.Enums;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class DeactivateUserHandlerTests
    {
        private AppDbContext _context = null!;
        private DeactivateUserHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new DeactivateUserHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ValidUser_DeactivatesUserAndLocksAuth()
        {
            // Arrange
            var user = TestDataFactory.SeedUser(_context, isActive: true);
            var request = new DeactivateUserRequestModel 
            { 
                UserId = user.UserID, 
                HospitalId = Guid.NewGuid(),
                PerformedByUserId = Guid.NewGuid()
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            
            var updatedUser = await _context.Users.FindAsync(user.UserID);
            Assert.That(updatedUser.UserStatusId, Is.EqualTo((int)UserStatusEnum.Revoked));

            var updatedAuth = await _context.UserAuths.FirstOrDefaultAsync(ua => ua.UserID == user.UserID);
            Assert.That(updatedAuth.IsLocked, Is.True);
            Assert.That(updatedAuth.UserStatusId, Is.EqualTo((int)UserStatusEnum.Revoked));
        }

        [Test]
        public async Task Handle_UserNotFound_ReturnsFailure()
        {
            // Arrange
            var request = new DeactivateUserRequestModel { UserId = Guid.NewGuid() };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("User not found"));
        }
    }
}
