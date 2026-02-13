using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.Extensions.Configuration;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class DeletePrescriptionAttachmentHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<IBlobStorageService> _mockBlobStorageService = null!;
        private Mock<IConfiguration> _mockConfiguration = null!;
        private DeletePrescriptionAttachmentHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _mockBlobStorageService = new Mock<IBlobStorageService>();
            _mockConfiguration = new Mock<IConfiguration>();
            
            _mockConfiguration.Setup(x => x["BlobStorage:PrescriptionAttachmentsContainer"]).Returns("test-container");

            _handler = new DeletePrescriptionAttachmentHandler(_context, _mockBlobStorageService.Object, _mockConfiguration.Object);
        }

        [TearDown]
        public void TearDown()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        [Test]
        public async Task Handle_AttachmentNotFound_ReturnsError()
        {
            // Arrange
            var request = new DeletePrescriptionAttachmentRequestModel
            {
                AttachmentId = Guid.NewGuid()
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Attachment not found."));
        }

        [Test]
        public async Task Handle_BlobStorageDeleteFailed_ReturnsError()
        {
            // Arrange
            var attachmentId = Guid.NewGuid();
            var doctorId = Guid.NewGuid();
            var hospitalId = Guid.NewGuid();
            var appointmentId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var patientId = "PAT123";

            _context.Users.Add(TestEntityFactory.CreateUser(userId));
            _context.Doctors.Add(TestEntityFactory.CreateDoctor(doctorId, userId));
            _context.Hospitals.Add(TestEntityFactory.CreateHospital(hospitalId, userId));
            _context.PrescriptionAttachments.Add(TestEntityFactory.CreatePrescriptionAttachment(attachmentId, appointmentId, doctorId, hospitalId, patientId));
            await _context.SaveChangesAsync();

            _mockBlobStorageService.Setup(x => x.DeleteAsync(attachmentId.ToString(), "test-container", It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var request = new DeletePrescriptionAttachmentRequestModel
            {
                AttachmentId = attachmentId
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Failed to delete attachment from blob storage."));
        }

        [Test]
        public async Task Handle_Success_DeletesAttachment()
        {
            // Arrange
            var attachmentId = Guid.NewGuid();
            var doctorId = Guid.NewGuid();
            var hospitalId = Guid.NewGuid();
            var appointmentId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var patientId = "PAT123";

            _context.Users.Add(TestEntityFactory.CreateUser(userId));
            _context.Doctors.Add(TestEntityFactory.CreateDoctor(doctorId, userId));
            _context.Hospitals.Add(TestEntityFactory.CreateHospital(hospitalId, userId));
            _context.PrescriptionAttachments.Add(TestEntityFactory.CreatePrescriptionAttachment(attachmentId, appointmentId, doctorId, hospitalId, patientId));
            await _context.SaveChangesAsync();

            _mockBlobStorageService.Setup(x => x.DeleteAsync(attachmentId.ToString(), "test-container", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var request = new DeletePrescriptionAttachmentRequestModel
            {
                AttachmentId = attachmentId
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.Message, Is.EqualTo("Attachment deleted successfully."));
            
            var deletedAttachment = await _context.PrescriptionAttachments.FindAsync(attachmentId);
            Assert.That(deletedAttachment, Is.Null);
        }
    }
}
