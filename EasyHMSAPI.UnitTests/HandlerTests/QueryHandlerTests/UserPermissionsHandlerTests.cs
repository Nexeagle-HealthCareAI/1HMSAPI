using System;
using System.Collections.Generic;
using System.Linq;
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
            
            // Get the role that was created by SeedUser
            var role = _context.Roles.First(r => r.RoleName == "Admin");
            
            // Add permissions to the role
            var perm = new RolePermission { RoleID = role.RoleID, PermissionKey = "Access" };
            _context.RolePermissions.Add(perm);
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
        public async Task Handle_ExcludesPermissionsWhereIsAllowedIsFalse()
        {
            // Arrange
            var user = TestDataFactory.SeedUser(_context, role: "Admin");
            user.UserStatusId = (int)UserStatusEnum.Active;

            var role = _context.Roles.First(r => r.RoleName == "Admin");
            _context.RolePermissions.Add(new RolePermission { RoleID = role.RoleID, PermissionKey = "granted", IsAllowed = true });
            _context.RolePermissions.Add(new RolePermission { RoleID = role.RoleID, PermissionKey = "revoked", IsAllowed = false });
            await _context.SaveChangesAsync();

            var request = new UserPermissionsRequestModel { UserId = user.UserID };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response, Is.Not.Null);
            Assert.That(response!.PermissionKeys, Does.Contain("granted"));
            Assert.That(response.PermissionKeys, Does.Not.Contain("revoked"));
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
