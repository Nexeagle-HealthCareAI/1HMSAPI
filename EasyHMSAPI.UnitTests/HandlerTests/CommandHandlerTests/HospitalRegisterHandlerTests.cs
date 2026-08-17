using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModel;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class HospitalRegisterHandlerTests
    {
        private AppDbContext _context = null!;
        private HospitalRegisterHandler _handler = null!;

        private class FakeHttpMessageHandler : HttpMessageHandler
        {
            private readonly HttpStatusCode _statusCode;
            private readonly string _content;
            private readonly Exception? _throws;

            public FakeHttpMessageHandler(HttpStatusCode statusCode, string content)
            {
                _statusCode = statusCode;
                _content = content;
            }

            public FakeHttpMessageHandler(Exception throws)
            {
                _throws = throws;
                _content = string.Empty;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                if (_throws != null) throw _throws;
                return Task.FromResult(new HttpResponseMessage(_statusCode) { Content = new StringContent(_content) });
            }
        }

        private class FakeHttpClientFactory : IHttpClientFactory
        {
            private readonly HttpMessageHandler _handler;
            public FakeHttpClientFactory(HttpMessageHandler handler) => _handler = handler;
            public HttpClient CreateClient(string name) => new HttpClient(_handler);
        }

        private static IConfiguration BuildConfiguration()
        {
            var mock = new Mock<IConfiguration>();
            mock.Setup(c => c["Cms:BaseUrl"]).Returns("http://fake-cms");
            mock.Setup(c => c["Cms:ServiceApiKey"]).Returns("test-service-key");
            return mock.Object;
        }

        private static HospitalRegisterHandler BuildHandler(AppDbContext context, HttpMessageHandler httpHandler)
        {
            return new HospitalRegisterHandler(
                context,
                new FakeHttpClientFactory(httpHandler),
                BuildConfiguration(),
                new Mock<ILogger<HospitalRegisterHandler>>().Object);
        }

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            // Unused by the two pre-existing tests below (they never set ReferralCode, so the
            // handler never dials out) -- a harmless default for tests that don't care.
            _handler = BuildHandler(_context, new FakeHttpMessageHandler(HttpStatusCode.OK, "{\"valid\":false}"));
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ValidRequest_RegistersHospital()
        {
            // Arrange
            var user = TestDataFactory.SeedUser(_context);
            var userProfile = new UserProfile { UserID = user.UserID, FullName = "Test User", UserStatusId = 1, EmployeeID = "EMP001" };
            _context.UserProfiles.Add(userProfile);
            await _context.SaveChangesAsync();

            var request = new HospitalRegisterRequestModel
            {
                UserId = user.UserID,
                Name = "Grand Hospital",
                Type = "General",
                Email = "info@grand.com",
                Contact = "9876543210",
                Location = "Down Town",
                City = "Metropolis",
                State = "NY",
                Country = "USA",
                Pincode = "10001",
                RegistrationNumber = "REG123"
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.HospitalId, Is.Not.Null);
            
            var hospital = await _context.Hospitals.FindAsync(response.HospitalId);
            Assert.That(hospital, Is.Not.Null);
            Assert.That(hospital!.Name, Is.EqualTo("Grand Hospital"));

            var hospitalUser = await _context.HospitalUsers.FirstOrDefaultAsync(hu => hu.HospitalID == response.HospitalId);
            Assert.That(hospitalUser, Is.Not.Null);
            Assert.That(hospitalUser!.IsPrimary, Is.True);
        }

        [Test]
        public async Task Handle_ValidRequest_ClonesHospitalScopedRoleInsteadOfMutatingGlobalRole()
        {
            // Arrange -- global "AdminDoctor" Role (HospitalID == null), the shared template
            // every registering user with this RoleName links to (see UserRegistrationHandler).
            var user = TestDataFactory.SeedUser(_context, role: "AdminDoctor");
            var userProfile = new UserProfile { UserID = user.UserID, FullName = "Test User", UserStatusId = 1, EmployeeID = "EMP001" };
            _context.UserProfiles.Add(userProfile);

            var globalRole = _context.Roles.Single(r => r.RoleName == "AdminDoctor");
            _context.RolePermissions.Add(new RolePermission { RoleID = globalRole.RoleID, PermissionKey = "ipd", IsAllowed = true });
            _context.RolePermissions.Add(new RolePermission { RoleID = globalRole.RoleID, PermissionKey = "inventory", IsAllowed = true });
            await _context.SaveChangesAsync();

            var request = new HospitalRegisterRequestModel
            {
                UserId = user.UserID,
                Name = "Grand Hospital",
                Type = "General",
                Email = "info@grand.com",
                Contact = "9876543210",
                Location = "Down Town",
                City = "Metropolis",
                State = "NY",
                Country = "USA",
                Pincode = "10001",
                RegistrationNumber = "REG123"
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);

            // The global role itself must be untouched -- still HospitalID == null, still
            // carrying its own permissions, still available as a template for the NEXT
            // hospital to clone from. This is the exact bug: it used to get mutated in place.
            var reloadedGlobalRole = await _context.Roles.FindAsync(globalRole.RoleID);
            Assert.That(reloadedGlobalRole!.HospitalID, Is.Null);
            var globalPerms = await _context.RolePermissions
                .Where(rp => rp.RoleID == globalRole.RoleID).Select(rp => rp.PermissionKey).ToListAsync();
            Assert.That(globalPerms, Is.EquivalentTo(new[] { "ipd", "inventory" }));

            // The user's UserRole must now point at a NEW, hospital-scoped clone carrying the
            // same permissions the global role had at clone time.
            var userRole = await _context.UserRoles.Include(ur => ur.Role).SingleAsync(ur => ur.UserID == user.UserID);
            Assert.That(userRole.RoleID, Is.Not.EqualTo(globalRole.RoleID));
            Assert.That(userRole.Role.HospitalID, Is.EqualTo(response.HospitalId));
            Assert.That(userRole.Role.RoleName, Is.EqualTo("AdminDoctor"));

            var hospitalRolePerms = await _context.RolePermissions
                .Where(rp => rp.RoleID == userRole.RoleID).Select(rp => rp.PermissionKey).ToListAsync();
            Assert.That(hospitalRolePerms, Is.EquivalentTo(new[] { "ipd", "inventory" }));
        }

        [Test]
        public async Task Handle_UserNotFound_ReturnsFailure()
        {
            // Arrange
            var request = new HospitalRegisterRequestModel { UserId = Guid.NewGuid() };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("User not found."));
        }

        private async Task<HospitalRegisterRequestModel> SeedUserAndBuildRequestAsync(string? referralCode)
        {
            var user = TestDataFactory.SeedUser(_context);
            var userProfile = new UserProfile { UserID = user.UserID, FullName = "Test User", UserStatusId = 1, EmployeeID = "EMP001" };
            _context.UserProfiles.Add(userProfile);
            await _context.SaveChangesAsync();

            return new HospitalRegisterRequestModel
            {
                UserId = user.UserID,
                Name = "Grand Hospital",
                Type = "General",
                Email = "info@grand.com",
                Contact = "9876543210",
                Location = "Down Town",
                City = "Metropolis",
                State = "NY",
                Country = "USA",
                Pincode = "10001",
                RegistrationNumber = "REG123",
                ReferralCode = referralCode
            };
        }

        [Test]
        public async Task Handle_ValidReferralCode_SnapshotsRewardOnTrialSubscription()
        {
            // Arrange
            var request = await SeedUserAndBuildRequestAsync("WELCOME5");
            var handler = BuildHandler(_context, new FakeHttpMessageHandler(
                HttpStatusCode.OK,
                "{\"valid\":true,\"rewardKind\":\"PercentageOff\",\"rewardValue\":5.00,\"referralCodeTypeName\":\"Launch Promo\"}"));

            // Act
            var response = await handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.ReferralCodeApplied, Is.True);

            var sub = await _context.HospitalSubscriptions.FirstOrDefaultAsync(s => s.HospitalId == response.HospitalId);
            Assert.That(sub, Is.Not.Null);
            Assert.That(sub!.ReferralCode, Is.EqualTo("WELCOME5"));
            Assert.That(sub.ReferralCodeRewardKind, Is.EqualTo("PercentageOff"));
            Assert.That(sub.ReferralCodeRewardValue, Is.EqualTo(5.00m));
        }

        [Test]
        public async Task Handle_InvalidReferralCode_StillRegistersHospitalWithoutReferral()
        {
            // Arrange
            var request = await SeedUserAndBuildRequestAsync("EXPIRED1");
            var handler = BuildHandler(_context, new FakeHttpMessageHandler(
                HttpStatusCode.OK,
                "{\"valid\":false,\"message\":\"Referral code has already been used.\"}"));

            // Act
            var response = await handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.ReferralCodeApplied, Is.False);
            Assert.That(response.ReferralCodeMessage, Is.EqualTo("Referral code has already been used."));

            var sub = await _context.HospitalSubscriptions.FirstOrDefaultAsync(s => s.HospitalId == response.HospitalId);
            Assert.That(sub, Is.Not.Null);
            Assert.That(sub!.ReferralCode, Is.Null);
        }

        [Test]
        public async Task Handle_CmsUnreachable_StillRegistersHospitalWithoutBlocking()
        {
            // Arrange
            var request = await SeedUserAndBuildRequestAsync("ANYCODE");
            var handler = BuildHandler(_context, new FakeHttpMessageHandler(new HttpRequestException("connection refused")));

            // Act
            var response = await handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.ReferralCodeApplied, Is.False);

            var sub = await _context.HospitalSubscriptions.FirstOrDefaultAsync(s => s.HospitalId == response.HospitalId);
            Assert.That(sub, Is.Not.Null);
            Assert.That(sub!.ReferralCode, Is.Null);
        }
    }
}
