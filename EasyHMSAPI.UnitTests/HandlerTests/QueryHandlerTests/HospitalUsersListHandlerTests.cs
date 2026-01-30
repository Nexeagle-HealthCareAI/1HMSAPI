using System;
using System.Collections.Generic;
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
    public class HospitalUsersListHandlerTests
    {
        private AppDbContext _context = null!;
        private HospitalUsersListHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new HospitalUsersListHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ReturnsUsersList()
        {
            // Arrange
            var hospitalId = Guid.NewGuid();
            var user = TestDataFactory.SeedUser(_context);
            user.UserStatusId = (int)UserStatusEnum.Active;
            
            var userProfile = new UserProfile { UserID = user.UserID, FullName = "User One", UserStatusId = (int)UserStatusEnum.Active };
            _context.UserProfiles.Add(userProfile);

            var hospitalUser = new HospitalUser { HospitalUserID = Guid.NewGuid(), HospitalID = hospitalId, UserID = user.UserID, IsPrimary = true };
            _context.HospitalUsers.Add(hospitalUser);
            
            await _context.SaveChangesAsync();

            var request = new HospitalUsersListRequestModel { HospitalId = hospitalId };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Users, Has.Count.EqualTo(1));
            Assert.That(response.Users[0].FullName, Is.EqualTo("User One"));
            Assert.That(response.Users[0].IsPrimary, Is.True);
        }

        [Test]
        public async Task Handle_NoUsers_ReturnsEmptyList()
        {
            // Arrange
            var request = new HospitalUsersListRequestModel { HospitalId = Guid.NewGuid() };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Users, Is.Empty);
        }
    }
}
