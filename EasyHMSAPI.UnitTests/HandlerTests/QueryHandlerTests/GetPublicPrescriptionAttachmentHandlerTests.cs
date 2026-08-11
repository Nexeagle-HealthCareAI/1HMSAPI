using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.Extensions.Configuration;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class GetPublicPrescriptionAttachmentHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<IBlobStorageService> _blobStorageServiceMock = null!;
        private GetPublicPrescriptionAttachmentHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _blobStorageServiceMock = new Mock<IBlobStorageService>();
            _blobStorageServiceMock
                .Setup(b => b.RefreshUrlAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("https://signed-url.example.com/rx.pdf");
            _handler = new GetPublicPrescriptionAttachmentHandler(_context, _blobStorageServiceMock.Object, new Mock<IConfiguration>().Object);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        [Test]
        public async Task Handle_EmptyId_ReturnsFailure()
        {
            var response = await _handler.Handle(new GetPublicPrescriptionAttachmentRequestModel { AttachmentId = Guid.Empty }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
        }

        [Test]
        public async Task Handle_AttachmentNotFound_ReturnsFailure()
        {
            var response = await _handler.Handle(new GetPublicPrescriptionAttachmentRequestModel { AttachmentId = Guid.NewGuid() }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("No prescription"));
        }

        [Test]
        public async Task Handle_AttachmentIsNotAPrescription_ReturnsFailure()
        {
            var attachmentId = Guid.NewGuid();
            _context.PrescriptionAttachments.Add(new PrescriptionAttachment
            {
                AttachmentId = attachmentId, ReportType = "Lab Report", StorageUrl = "http://old-url.com/report.pdf", FileName = "report.pdf",
            });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetPublicPrescriptionAttachmentRequestModel { AttachmentId = attachmentId }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("No prescription"));
        }

        [Test]
        public async Task Handle_ValidPrescriptionAttachment_ReturnsRedirectUrl()
        {
            var attachmentId = Guid.NewGuid();
            _context.PrescriptionAttachments.Add(new PrescriptionAttachment
            {
                AttachmentId = attachmentId, ReportType = "Prescription", StorageUrl = "http://old-url.com/rx.pdf", FileName = "rx.pdf",
            });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetPublicPrescriptionAttachmentRequestModel { AttachmentId = attachmentId }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.RedirectUrl, Is.EqualTo("https://signed-url.example.com/rx.pdf"));
            Assert.That(response.FileName, Is.EqualTo("rx.pdf"));
        }
    }
}
