using System;
using Moq;
using NUnit.Framework;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.Services.Interfaces;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class DeleteProfilePictureHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<IBlobStorageService> _blobServiceMock = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _blobServiceMock = new Mock<IBlobStorageService>();
        }

        [TearDown]
        public void TearDown()
        {
            _context?.Dispose();
            InMemoryDbContextFactory.Destroy(_context);
        }

        //[Test, Ignore("TODO: Implement test logic")]
        //public void Constructor_Smoke()
        //{
        //    var handler = new DeleteProfilePictureHandler(_context, _blobServiceMock.Object);
        //    Assert.That(handler, Is.Not.Null);
        //}
    }
}
