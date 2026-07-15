using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class UploadImageCommandHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<IBlobStorageService> _blobStorageServiceMock = null!;
        private Mock<IConfiguration> _configurationMock = null!;
        private UploadImageCommandHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _blobStorageServiceMock = new Mock<IBlobStorageService>();
            _configurationMock = new Mock<IConfiguration>();
            
            _configurationMock.SetupGet(x => x["BlobStorage:ProfilePhotosContainer"]).Returns("profile-photos");

            _handler = new UploadImageCommandHandler(
                _configurationMock.Object, 
                _blobStorageServiceMock.Object, 
                _context);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ValidUser_UploadsPicture()
        {
             // Arrange
            var user = TestDataFactory.SeedUser(_context);
            var userProfile = new UserProfile { UserID = user.UserID, FullName = "Test User" };
            _context.UserProfiles.Add(userProfile);
            await _context.SaveChangesAsync();

            var fileMock = new Mock<IFormFile>();
            _blobStorageServiceMock.Setup(x => x.UploadAsync(It.IsAny<string>(), It.IsAny<IFormFile>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("http://blob.url/photo.jpg");

            var request = new UploadProfilePictureRequestModel
            {
                UserId = user.UserID,
                File = fileMock.Object
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.ProfilePictureUrl, Is.EqualTo("http://blob.url/photo.jpg"));
            
            var updatedProfile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserID == user.UserID);
            Assert.That(updatedProfile!.ProfilePictureURL, Is.EqualTo("http://blob.url/photo.jpg"));
        }

        [Test]
        public async Task Handle_UserNotFound_ReturnsFailure()
        {
            // Arrange
            var request = new UploadProfilePictureRequestModel { UserId = Guid.NewGuid() };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
        }

        [Test]
        public async Task Handle_AdminUploadForDoctorNotAtSpecifiedHospital_RejectsUpload()
        {
            // Arrange — admin (Public Directory tile editor) passes HospitalId, but the target
            // doctor has no DoctorDepartment row at that hospital.
            var user = TestDataFactory.SeedUser(_context);
            var userProfile = new UserProfile { UserID = user.UserID, FullName = "Test User" };
            _context.UserProfiles.Add(userProfile);
            TestDataFactory.SeedDoctor(_context, user);
            await _context.SaveChangesAsync();

            var fileMock = new Mock<IFormFile>();
            var request = new UploadProfilePictureRequestModel
            {
                UserId = user.UserID,
                File = fileMock.Object,
                HospitalId = Guid.NewGuid(),
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            _blobStorageServiceMock.Verify(x => x.UploadAsync(It.IsAny<string>(), It.IsAny<IFormFile>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task Handle_AdminUploadForDoctorAtOwnHospital_AllowsUpload()
        {
            // Arrange
            var user = TestDataFactory.SeedUser(_context);
            var userProfile = new UserProfile { UserID = user.UserID, FullName = "Test User" };
            _context.UserProfiles.Add(userProfile);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            var hospital = TestDataFactory.SeedHospital(_context, user.UserID);
            TestDataFactory.SeedDoctorDepartment(_context, doctor.DoctorID, hospital.HospitalID);
            await _context.SaveChangesAsync();

            var fileMock = new Mock<IFormFile>();
            _blobStorageServiceMock.Setup(x => x.UploadAsync(It.IsAny<string>(), It.IsAny<IFormFile>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("http://blob.url/photo.jpg");

            var request = new UploadProfilePictureRequestModel
            {
                UserId = user.UserID,
                File = fileMock.Object,
                HospitalId = hospital.HospitalID,
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.ProfilePictureUrl, Is.EqualTo("http://blob.url/photo.jpg"));
        }
    }
}
