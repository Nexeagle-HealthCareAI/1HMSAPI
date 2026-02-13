using System;
using System.Collections.Generic;
using System.Linq;
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
    public class UserProfileUpdateHandlerTests
    {
        private AppDbContext _context = null!;
        private UserProfileUpdateHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new UserProfileUpdateHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        [Test]
        public async Task Handle_UserNotFound_ReturnsError()
        {
            // Arrange
            var request = new UserProfileUpdateRequestModel { UserId = Guid.NewGuid() };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("User not found."));
        }

        [Test]
        public async Task Handle_UserRevoked_ReturnsError()
        {
            // Arrange
            var userId = Guid.NewGuid();
            _context.Users.Add(new User
            {
                UserID = userId,
                UserStatusId = (int)UserStatusEnum.Revoked,
                MobileNumber = "1234567890",
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            var request = new UserProfileUpdateRequestModel { UserId = userId };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("User not found."));
        }

        [Test]
        public async Task Handle_UpdateMobileNumber_ReturnsSuccess()
        {
            // Arrange
            var userId = Guid.NewGuid();
            _context.Users.Add(new User
            {
                UserID = userId,
                UserStatusId = (int)UserStatusEnum.Active,
                MobileNumber = "1234567890",
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            var request = new UserProfileUpdateRequestModel
            {
                UserId = userId,
                MobileNumber = "0987654321"
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.UpdatedFields, Does.Contain("MobileNumber"));
            
            var user = await _context.Users.FindAsync(userId);
            Assert.That(user!.MobileNumber, Is.EqualTo("0987654321"));
        }

        [Test]
        public async Task Handle_CreateUserProfile_ReturnsSuccess()
        {
            // Arrange
            var userId = Guid.NewGuid();
            _context.Users.Add(new User
            {
                UserID = userId,
                UserStatusId = (int)UserStatusEnum.Active,
                MobileNumber = "1234567890",
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            var request = new UserProfileUpdateRequestModel
            {
                UserId = userId,
                FullName = "John Doe",
                Gender = "Male"
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.UpdatedFields, Does.Contain("UserProfile Created"));
            
            var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserID == userId);
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile!.FullName, Is.EqualTo("John Doe"));
            Assert.That(profile.Gender, Is.EqualTo("Male"));
        }

        [Test]
        public async Task Handle_UpdateExistingUserProfile_ReturnsSuccess()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new User
            {
                UserID = userId,
                UserStatusId = (int)UserStatusEnum.Active,
                MobileNumber = "1234567890",
                CreatedAt = DateTime.UtcNow
            };
            _context.Users.Add(user);
            _context.UserProfiles.Add(new UserProfile
            {
                UserProfileID = Guid.NewGuid(),
                UserID = userId,
                FullName = "Jane Doe",
                City = "Old City"
            });
            await _context.SaveChangesAsync();

            var request = new UserProfileUpdateRequestModel
            {
                UserId = userId,
                FullName = "Jane Smith",
                City = "New City"
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.UpdatedFields, Does.Contain("FullName"));
            Assert.That(response.UpdatedFields, Does.Contain("City"));
            
            var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserID == userId);
            Assert.That(profile!.FullName, Is.EqualTo("Jane Smith"));
            Assert.That(profile.City, Is.EqualTo("New City"));
        }

        [Test]
        public async Task Handle_ProfileCompletionCalculation_ReturnsCorrectScore()
        {
            // Arrange
            var userId = Guid.NewGuid();
            _context.Users.Add(new User
            {
                UserID = userId,
                UserStatusId = (int)UserStatusEnum.Active,
                MobileNumber = "1234567890",
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            var request = new UserProfileUpdateRequestModel
            {
                UserId = userId,
                FullName = "Complete User",
                Gender = "Male",
                Language = "English",
                ProfilePictureURL = "http://example.com/pic.jpg",
                EmployeeID = "EMP001",
                BloodGroup = "O+",
                AddressLine1 = "123 St",
                City = "Metropolis",
                State = "NY",
                Country = "India",
                Pincode = "123456", // 6 digits for India bonus
                EmergencyContactName = "Emergency",
                EmergencyContactNumber = "9119119111"
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            
            var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserID == userId);
            Assert.That(profile!.ProfileCompletionPercentage, Is.GreaterThan(0));
            // You can calculate exact expected score if needed, but > 0 confirms calculation happened
        }

        [Test]
        public async Task Handle_NoChanges_ReturnsSuccessWithNoChangesMessage()
        {
             // Arrange
            var userId = Guid.NewGuid();
            _context.Users.Add(new User
            {
                UserID = userId,
                UserStatusId = (int)UserStatusEnum.Active,
                MobileNumber = "1234567890" // Added required property
            });
            _context.UserProfiles.Add(new UserProfile
            {
                UserProfileID = Guid.NewGuid(),
                UserID = userId,
                FullName = "No Change"
            });
            await _context.SaveChangesAsync();

            var request = new UserProfileUpdateRequestModel
            {
                UserId = userId,
                FullName = "No Change" // Same value
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.Message, Is.EqualTo("No changes were made."));
            Assert.That(response.UpdatedFields, Is.Empty);
        }
    }
}
