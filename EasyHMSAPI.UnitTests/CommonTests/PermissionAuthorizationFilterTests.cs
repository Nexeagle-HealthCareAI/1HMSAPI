using System;
using System.Linq;
using System.Threading.Tasks;
using EasyHMSAPI.Api.Common;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.Extensions.Caching.Memory;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.CommonTests
{
    [TestFixture]
    public class PermissionAuthorizationFilterTests
    {
        private EasyHMSAPI.Domain.Context.AppDbContext _context = null!;
        private IMemoryCache _cache = null!;
        private PermissionAuthorizationFilter _filter = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _cache = new MemoryCache(new MemoryCacheOptions());
            _filter = new PermissionAuthorizationFilter(_context, _cache);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
            _cache.Dispose();
        }

        [Test]
        public async Task ResolveGrantedPermissionsAsync_ReturnsKeysGrantedViaTheCallersRoles()
        {
            var user = TestDataFactory.SeedUser(_context, role: "Doctor");
            var role = _context.Roles.First(r => r.RoleName == "Doctor");
            _context.RolePermissions.Add(new RolePermission { RoleID = role.RoleID, PermissionKey = "doc_board", IsAllowed = true });
            _context.RolePermissions.Add(new RolePermission { RoleID = role.RoleID, PermissionKey = "ipd", IsAllowed = true });
            await _context.SaveChangesAsync();

            var granted = await _filter.ResolveGrantedPermissionsAsync(user.UserID);

            Assert.That(granted, Is.EquivalentTo(new[] { "doc_board", "ipd" }));
        }

        [Test]
        public async Task ResolveGrantedPermissionsAsync_ExcludesRevokedPermissions()
        {
            var user = TestDataFactory.SeedUser(_context, role: "Doctor");
            var role = _context.Roles.First(r => r.RoleName == "Doctor");
            _context.RolePermissions.Add(new RolePermission { RoleID = role.RoleID, PermissionKey = "doc_board", IsAllowed = true });
            _context.RolePermissions.Add(new RolePermission { RoleID = role.RoleID, PermissionKey = "billing", IsAllowed = false });
            await _context.SaveChangesAsync();

            var granted = await _filter.ResolveGrantedPermissionsAsync(user.UserID);

            Assert.That(granted, Does.Contain("doc_board"));
            Assert.That(granted, Does.Not.Contain("billing"));
        }

        [Test]
        public async Task ResolveGrantedPermissionsAsync_ReturnsEmptySetForAUserWithNoRoles()
        {
            var userId = Guid.NewGuid(); // never seeded into UserRoles

            var granted = await _filter.ResolveGrantedPermissionsAsync(userId);

            Assert.That(granted, Is.Empty);
        }

        [Test]
        public async Task ResolveGrantedPermissionsAsync_CachesWithinTheTtl_SoARevocationDuringTheWindowIsntSeenYet()
        {
            var user = TestDataFactory.SeedUser(_context, role: "Doctor");
            var role = _context.Roles.First(r => r.RoleName == "Doctor");
            var permission = new RolePermission { RoleID = role.RoleID, PermissionKey = "doc_board", IsAllowed = true };
            _context.RolePermissions.Add(permission);
            await _context.SaveChangesAsync();

            var first = await _filter.ResolveGrantedPermissionsAsync(user.UserID);
            Assert.That(first, Does.Contain("doc_board"));

            // Revoke directly in the DB, bypassing the filter -- if ResolveGrantedPermissionsAsync
            // re-queried instead of serving the cached result, this would now come back excluded.
            permission.IsAllowed = false;
            await _context.SaveChangesAsync();

            var second = await _filter.ResolveGrantedPermissionsAsync(user.UserID);
            Assert.That(second, Does.Contain("doc_board"), "expected the 60s cache to still serve the pre-revocation result");
        }
    }
}
