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

            var request = new UserPermissionsRequestModel { UserId = user.UserID, CallerUserId = user.UserID };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response, Is.Not.Null);
            Assert.That(response!.Forbidden, Is.False);
            Assert.That(response.RoleName, Is.EqualTo("Admin"));
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

            var request = new UserPermissionsRequestModel { UserId = user.UserID, CallerUserId = user.UserID };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response, Is.Not.Null);
            Assert.That(response!.PermissionKeys, Does.Contain("granted"));
            Assert.That(response.PermissionKeys, Does.Not.Contain("revoked"));
        }

        [Test]
        public async Task Handle_CallerQueriesSomeoneElse_WithoutAdminPanel_ReturnsForbidden()
        {
            var target = TestDataFactory.SeedUser(_context, role: "Doctor", email: "target@example.com", phone: "1111111111");
            var caller = TestDataFactory.SeedUser(_context, role: "Receptionist", email: "caller@example.com", phone: "2222222222");
            await _context.SaveChangesAsync();

            var request = new UserPermissionsRequestModel { UserId = target.UserID, CallerUserId = caller.UserID };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response, Is.Not.Null);
            Assert.That(response!.Forbidden, Is.True);
            Assert.That(response.PermissionKeys, Is.Null);
        }

        [Test]
        public async Task Handle_CallerQueriesSomeoneElse_WithAdminPanel_Succeeds()
        {
            var target = TestDataFactory.SeedUser(_context, role: "Doctor", email: "target2@example.com", phone: "3333333333");
            var caller = TestDataFactory.SeedUser(_context, role: "Admin", email: "caller2@example.com", phone: "4444444444");
            var adminRole = _context.Roles.First(r => r.RoleName == "Admin");
            _context.RolePermissions.Add(new RolePermission { RoleID = adminRole.RoleID, PermissionKey = "admin_panel", IsAllowed = true });
            await _context.SaveChangesAsync();

            var request = new UserPermissionsRequestModel { UserId = target.UserID, CallerUserId = caller.UserID };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response, Is.Not.Null);
            Assert.That(response!.Forbidden, Is.False);
        }

        [Test]
        public async Task Handle_UserHoldsMultipleRoles_ReturnsUnionOfDistinctPermissionKeys()
        {
            // Regression guard for the multi-role union (SelectMany(...).Distinct()) this
            // handler leans on -- e.g. a real user holding both Receptionist and Nurse.
            var user = TestDataFactory.SeedUser(_context, role: "Receptionist");
            var nurseRole = new Role { RoleID = Guid.NewGuid(), RoleName = "Nurse", IsSystemDefined = true, IsActive = true, CreatedAt = DateTime.UtcNow };
            _context.Roles.Add(nurseRole);
            _context.UserRoles.Add(new UserRole { UserID = user.UserID, RoleID = nurseRole.RoleID });

            var receptionistRole = _context.Roles.First(r => r.RoleName == "Receptionist");
            _context.RolePermissions.Add(new RolePermission { RoleID = receptionistRole.RoleID, PermissionKey = "appointment_scheduler", IsAllowed = true });
            // Shared key on both roles -- Distinct() must collapse this to one entry.
            _context.RolePermissions.Add(new RolePermission { RoleID = receptionistRole.RoleID, PermissionKey = "shared_key", IsAllowed = true });
            _context.RolePermissions.Add(new RolePermission { RoleID = nurseRole.RoleID, PermissionKey = "nursing_station", IsAllowed = true });
            _context.RolePermissions.Add(new RolePermission { RoleID = nurseRole.RoleID, PermissionKey = "shared_key", IsAllowed = true });
            await _context.SaveChangesAsync();

            var request = new UserPermissionsRequestModel { UserId = user.UserID, CallerUserId = user.UserID };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response, Is.Not.Null);
            Assert.That(response!.PermissionKeys, Is.EquivalentTo(new[] { "appointment_scheduler", "nursing_station", "shared_key" }));
            Assert.That(response.RoleName, Does.Contain("Receptionist"));
            Assert.That(response.RoleName, Does.Contain("Nurse"));
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
