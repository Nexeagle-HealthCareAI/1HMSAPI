using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class UploadProfilePictureHandlerTests
    {
        //private AppDbContext _context = null!;
        //private Mock<IAssetStorageService> _storageServiceMock = null!;

        //[SetUp]
        //public void SetUp()
        //{
        //    _context = InMemoryDbContextFactory.CreateContext();
        //    _storageServiceMock = new Mock<IAssetStorageService>();
        //}

        //[TearDown]
        //public void TearDown()
        //{
        //    InMemoryDbContextFactory.Destroy(_context);
        //}

        //[Test, Ignore("TODO: Implement test logic")]
        //public void Constructor_Smoke()
        //{
        //    var handler = new UploadProfilePictureHandler(_context, _storageServiceMock.Object);
        //    Assert.That(handler, Is.Not.Null);
        //}

        //[Test]
        //public void Handle_ShouldUploadProfilePicture_WhenValidInput()
        //{
        //    // Arrange
        //    var pictureId = Guid.NewGuid();
        //    var handler = new UploadProfilePictureHandler(_context, _storageServiceMock.Object);

        //    // Act
        //    var result = handler.Handle(new UploadProfilePictureCommand { PictureId = pictureId });

        //    // Assert
        //    Assert.That(result, Is.True, "Profile picture should be uploaded successfully.");
        //}

        //[Test]
        //public void Handle_ShouldThrowException_WhenInvalidInput()
        //{
        //    // Arrange
        //    var handler = new UploadProfilePictureHandler(_context, _storageServiceMock.Object);

        //    // Act & Assert
        //    Assert.Throws<Exception>(() => handler.Handle(new UploadProfilePictureCommand()),
        //        "Expected exception when input is invalid.");
        //}
    }
}
