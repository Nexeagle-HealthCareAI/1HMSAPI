using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Domain.Context;
using Moq;
using NUnit.Framework;
using System;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class UploadAssetHandlerTests
    {
        //private AppDbContext _context = null!;
        //private Mock<IAssetStorageService> _storageServiceMock = null!;

        //[SetUp]
        //public void SetUp()
        //{
        //    _context = InMemoryDbContextFactory.CreateContext();
        //    _storageServiceMock = new Mock<IAssetStorageService>();
        //}

        [TearDown]
        public void TearDown()
        {
            //    InMemoryDbContextFactory.Destroy(_context);
            //_context?.Dispose();
        }

        //[Test, Ignore("TODO: Implement test logic")]
        //public void Constructor_Smoke()
        //{
        //    var handler = new UploadAssetHandler(_context, _storageServiceMock.Object);
        //    Assert.That(handler, Is.Not.Null);
        //}

        //[Test]
        //public void Handle_ShouldUploadAsset_WhenValidInput()
        //{
        //    // Arrange
        //    var assetId = Guid.NewGuid();
        //    var handler = new UploadAssetHandler(_context, _storageServiceMock.Object);

        //    // Act
        //    var result = handler.Handle(new UploadAssetCommand { AssetId = assetId });

        //    // Assert
        //    Assert.That(result, Is.True, "Asset should be uploaded successfully.");
        //}

        //[Test]
        //public void Handle_ShouldThrowException_WhenInvalidInput()
        //{
        //    // Arrange
        //    var handler = new UploadAssetHandler(_context, _storageServiceMock.Object);

        //    // Act & Assert
        //    Assert.Throws<Exception>(() => handler.Handle(new UploadAssetCommand()),
        //        "Expected exception when input is invalid.");
        //}
    }
}
