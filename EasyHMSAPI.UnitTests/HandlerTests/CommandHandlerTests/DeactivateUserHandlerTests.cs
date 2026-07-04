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

        // The handler authorizes on (a) caller belongs to the hospital and (b) caller is
        // Admin/AdminDoctor — both via HospitalUsers/UserRoles, not just a truthy CallerUserId.
        private void SeedHospitalMembership(Guid hospitalId, Guid userId, bool isPrimary = false)
        {
            _context.HospitalUsers.Add(new HospitalUser
            {
                HospitalUserID = Guid.NewGuid(),
                HospitalID = hospitalId,
                UserID = userId,
                IsPrimary = isPrimary,
            });
            _context.SaveChanges();
        }

        [Test]
        public async Task Handle_ValidUser_DeactivatesUserAndLocksAuth()
        {
            // Arrange
            var hospitalId = Guid.NewGuid();
            var caller = TestDataFactory.SeedUser(_context, email: "admin@example.com", phone: "1112223333", role: "Admin");
            var user = TestDataFactory.SeedUser(_context, isActive: true);
            SeedHospitalMembership(hospitalId, caller.UserID);
            SeedHospitalMembership(hospitalId, user.UserID);

            var request = new DeactivateUserRequestModel
            {
                UserId = user.UserID,
                HospitalId = hospitalId,
                PerformedByUserId = caller.UserID,
                CallerUserId = caller.UserID,
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
            // Arrange — a valid admin caller, and a target that's a hospital member (so it clears
            // the membership/owner checks) but has no matching Users row, isolating the specific
            // "user not found" branch under test.
            var hospitalId = Guid.NewGuid();
            var caller = TestDataFactory.SeedUser(_context, email: "admin2@example.com", phone: "4445556666", role: "Admin");
            var missingUserId = Guid.NewGuid();
            SeedHospitalMembership(hospitalId, caller.UserID);
            SeedHospitalMembership(hospitalId, missingUserId);

            var request = new DeactivateUserRequestModel
            {
                UserId = missingUserId,
                HospitalId = hospitalId,
                CallerUserId = caller.UserID,
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("User not found"));
        }
    }
}
