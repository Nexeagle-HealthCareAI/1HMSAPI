using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.Extensions.Configuration;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class DoctorGetHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<IBlobStorageService> _blobServiceMock = null!;
        private Mock<IConfiguration> _configurationMock = null!;
        private DoctorGetHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _blobServiceMock = new Mock<IBlobStorageService>();
            _blobServiceMock.Setup(x => x.GetUrlAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("http://example.com/photo.jpg");
            _configurationMock = new Mock<IConfiguration>();
            _configurationMock.Setup(c => c["BlobStorage:ProfilePhotosContainer"]).Returns("photos");

            _handler = new DoctorGetHandler(_context, _blobServiceMock.Object, _configurationMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ValidDoctorId_ReturnsDoctorDetails()
        {
            // Arrange
            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            var request = new DoctorGetRequestModel { UserId = user.UserID };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response, Is.Not.Null);
            Assert.That(response.UserId, Is.EqualTo(user.UserID));
            Assert.That(response.DoctorId, Is.EqualTo(doctor.DoctorID));
            Assert.That(response.LicenseNumber, Is.EqualTo(doctor.LicenseNumber));
        }

        [Test]
        public async Task Handle_DoctorWithPublicProfileFields_ReturnsLanguagesPhotoAndContactInfo()
        {
            // Arrange
            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user, isPubliclyListed: true);
            doctor.LanguagesJson = "[\"English\",\"Hindi\"]";
            doctor.PublicContactEmail = "doctor@example.com";
            doctor.PublicContactPhone = "9876543210";
            await _context.SaveChangesAsync();
            var request = new DoctorGetRequestModel { UserId = user.UserID };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response, Is.Not.Null);
            Assert.That(response!.Languages, Is.EquivalentTo(new[] { "English", "Hindi" }));
            Assert.That(response.PublicContactEmail, Is.EqualTo("doctor@example.com"));
            Assert.That(response.PublicContactPhone, Is.EqualTo("9876543210"));
            Assert.That(response.PhotoUrl, Is.EqualTo("http://example.com/photo.jpg"));
            Assert.That(response.IsPubliclyListed, Is.True);
        }

        [Test]
        public async Task Handle_InvalidUserId_ReturnsNull()
        {
            // Arrange
            var request = new DoctorGetRequestModel { UserId = Guid.NewGuid() };

             // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response, Is.Null);
        }
    }
}
