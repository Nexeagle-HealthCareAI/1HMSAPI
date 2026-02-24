using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class DeleteProfilePictureHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<IBlobStorageService> _blobStorageServiceMock = null!;
        private Mock<IConfiguration> _configurationMock = null!;
        private DeleteProfilePictureHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _blobStorageServiceMock = new Mock<IBlobStorageService>();
            _configurationMock = new Mock<IConfiguration>();
            
            _configurationMock.SetupGet(x => x["BlobStorage:ProfilePhotosContainer"]).Returns("profile-photos");

            _handler = new DeleteProfilePictureHandler(_configurationMock.Object, _blobStorageServiceMock.Object, _context);
        }

        [TearDown]
        public void TearDown()
        {
             
             InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ValidUser_DeletesPictureSuccessfully()
        {
            // Arrange
            var user = TestDataFactory.SeedUser(_context);
            var userProfile = new UserProfile { UserID = user.UserID, FullName = "Test User", UserStatusId = 1, ProfilePictureURL = "http://blob/pic.jpg" };
            _context.UserProfiles.Add(userProfile);
            await _context.SaveChangesAsync();

            _blobStorageServiceMock.Setup(x => x.DeleteAsync(user.UserID.ToString(), "profile-photos", It.IsAny<CancellationToken>()))
                 .ReturnsAsync(true);

            var request = new DeleteProfilePictureRequestModel { UserId = user.UserID };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            var updatedProfile = await _context.UserProfiles.FirstOrDefaultAsync(u => u.UserID == user.UserID);
            Assert.That(updatedProfile.ProfilePictureURL, Is.Empty);
        }

        [Test]
        public async Task Handle_UserNotFound_ReturnsFailure()
        {
            // Arrange
            var request = new DeleteProfilePictureRequestModel { UserId = Guid.NewGuid() };

             // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Invalid user Id"));
        }
    }
}
