using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Data.Enums;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class UserSearchHandlerTests
    {
        private AppDbContext _context = null!;
        private UserSearchHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new UserSearchHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ReturnsUserDetails()
        {
            // Arrange
            var user = TestDataFactory.SeedUser(_context);
            user.UserStatusId = (int)UserStatusEnum.Active;
            var auth = new UserAuth { UserAuthID = Guid.NewGuid(), UserID = user.UserID, LoginMethod = "1" };
            _context.UserAuths.Add(auth);
            var profile = new UserProfile { UserProfileID = Guid.NewGuid(), UserID = user.UserID, FullName = "User Test" };
            _context.UserProfiles.Add(profile);
            await _context.SaveChangesAsync();

            var request = new UserSearchRequestModel { UserId = user.UserID };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response, Is.Not.Null);
            Assert.That(response!.UserId, Is.EqualTo(user.UserID));
            Assert.That(response.UserProfile!.FullName, Is.EqualTo("User Test"));
        }

         [Test]
        public async Task Handle_NotFound_ReturnsNull()
        {
            // Arrange
            var request = new UserSearchRequestModel { UserId = Guid.NewGuid() };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response, Is.Null);
        }
    }
}
