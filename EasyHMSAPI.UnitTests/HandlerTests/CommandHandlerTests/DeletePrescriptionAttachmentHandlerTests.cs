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
    public class DeletePrescriptionAttachmentHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<IBlobStorageService> _blobStorageServiceMock = null!;
        private Mock<IConfiguration> _configurationMock = null!;
        private DeletePrescriptionAttachmentHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _blobStorageServiceMock = new Mock<IBlobStorageService>();
            _configurationMock = new Mock<IConfiguration>();
            
            _configurationMock.SetupGet(x => x["BlobStorage:PrescriptionAttachmentsContainer"]).Returns("prescriptions");

            _handler = new DeletePrescriptionAttachmentHandler(_context, _blobStorageServiceMock.Object, _configurationMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ValidAttachment_DeletesSuccessfully()
        {
            // Arrange
            var attachmentId = Guid.NewGuid();
            var attachment = new PrescriptionAttachment { AttachmentId = attachmentId, FileName = "test.jpg" };
            _context.PrescriptionAttachments.Add(attachment);
            await _context.SaveChangesAsync();

            _blobStorageServiceMock.Setup(x => x.DeleteAsync(attachmentId.ToString(), "prescriptions", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var request = new DeletePrescriptionAttachmentRequestModel { AttachmentId = attachmentId };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            var deleted = await _context.PrescriptionAttachments.FindAsync(attachmentId);
            Assert.That(deleted, Is.Null);
        }

        [Test]
        public async Task Handle_AttachmentNotFound_ReturnsFailure()
        {
             // Arrange
            var request = new DeletePrescriptionAttachmentRequestModel { AttachmentId = Guid.NewGuid() };

             // Act
            var response = await _handler.Handle(request, CancellationToken.None);

             // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Attachment not found."));
        }
    }
}
