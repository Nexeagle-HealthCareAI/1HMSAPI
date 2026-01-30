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
    public class UserPermissionsHandlerTests
    {
        private AppDbContext _context = null!;
        private UserPermissionsHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new UserPermissionsHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ReturnsUserPermissions()
        {
            // Arrange
            var user = TestDataFactory.SeedUser(_context, role: "Admin");
            user.UserStatusId = (int)UserStatusEnum.Active;
            
            var role = new Role { RoleID = Guid.NewGuid(), RoleName = "Admin" };
            _context.Roles.Add(role);
            var perm = new RolePermission { RoleID = role.RoleID, PermissionKey = "Access" };
            _context.RolePermissions.Add(perm);
            
            var userRole = new UserRole { UserID = user.UserID, RoleID = role.RoleID };
            _context.UserRoles.Add(userRole);
            await _context.SaveChangesAsync();

            var request = new UserPermissionsRequestModel { UserId = user.UserID };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response, Is.Not.Null);
            Assert.That(response!.RoleName, Is.EqualTo("Admin"));
            Assert.That(response.PermissionKeys, Does.Contain("Access"));
        }

         [Test]
        public async Task Handle_EmptyUserId_ReturnsAllRoles()
        {
             // Arrange
             var role = new Role { RoleID = Guid.NewGuid(), RoleName = "Admin" };
            _context.Roles.Add(role);
            await _context.SaveChangesAsync();

            var request = new UserPermissionsRequestModel { UserId = Guid.Empty };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response, Is.Not.Null);
            Assert.That(response!.AllRoles, Has.Count.EqualTo(1));
            Assert.That(response.AllRoles![0].RoleName, Is.EqualTo("Admin"));
        }
    }
}
