using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Data.Enums;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.Extensions.Configuration;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class GetProfilePictureHandlerTests
    {
         private AppDbContext _context = null!;
        private Mock<IBlobStorageService> _blobServiceMock = null!;
        private Mock<IConfiguration> _configurationMock = null!;
        private GetProfilePictureHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _blobServiceMock = new Mock<IBlobStorageService>();
            _configurationMock = new Mock<IConfiguration>();
            _configurationMock.Setup(c => c["BlobStorage:ProfilePhotosContainer"]).Returns("photos");

            _handler = new GetProfilePictureHandler(_configurationMock.Object, _blobServiceMock.Object, _context);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ReturnsProfilePictureUrl()
        {
            // Arrange
            var user = TestDataFactory.SeedUser(_context);
            user.UserStatusId = (int)UserStatusEnum.Active;
            await _context.SaveChangesAsync();

            _blobServiceMock.Setup(x => x.GetUrlAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("http://example.com/photo.jpg");

            var request = new GetProfilePictureRequestModel { UserId = user.UserID };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.ProfilePictureUrl, Is.EqualTo("http://example.com/photo.jpg"));
        }

        [Test]
        public async Task Handle_UserNotFound_ReturnsEmpty()
        {
             // Arrange
            var request = new GetProfilePictureRequestModel { UserId = Guid.NewGuid() };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.ProfilePictureUrl, Is.Empty);
        }
    }
}
