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
    public class GetPublicDischargeSummaryPdfHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<IBlobStorageService> _blobStorageServiceMock = null!;
        private GetPublicDischargeSummaryPdfHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _blobStorageServiceMock = new Mock<IBlobStorageService>();
            _blobStorageServiceMock
                .Setup(b => b.RefreshUrlAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("https://signed-url.example.com/discharge.pdf");
            _handler = new GetPublicDischargeSummaryPdfHandler(_context, _blobStorageServiceMock.Object, new Mock<IConfiguration>().Object);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private DischargeSummary SeedSummary(bool isSigned, string accessToken = "tok123", string? pdfBlobKey = "key123")
        {
            var summary = new DischargeSummary
            {
                DischargeSummaryId = Guid.NewGuid(), HospitalId = Guid.NewGuid(), AdmissionId = Guid.NewGuid(),
                IsSigned = isSigned, AccessToken = accessToken, PdfBlobKey = pdfBlobKey,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            _context.DischargeSummary.Add(summary);
            _context.SaveChanges();
            return summary;
        }

        [Test]
        public async Task Handle_SignedSummary_ReturnsRedirectUrl()
        {
            SeedSummary(isSigned: true);

            var response = await _handler.Handle(new GetPublicDischargeSummaryPdfRequestModel { AccessToken = "tok123" }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.RedirectUrl, Is.EqualTo("https://signed-url.example.com/discharge.pdf"));
        }

        [Test]
        public async Task Handle_UnsignedSummary_ReturnsFailure_NeverResolvesToDocument()
        {
            // The core of the "only if signed and finalized" requirement -- an unsigned draft's
            // link must not resolve to the document, even if a PdfBlobKey/AccessToken already exist
            // (e.g. from an earlier signed-then-unsigned cycle).
            SeedSummary(isSigned: false);

            var response = await _handler.Handle(new GetPublicDischargeSummaryPdfRequestModel { AccessToken = "tok123" }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("not yet finalized"));
            Assert.That(response.RedirectUrl, Is.Null);
        }

        [Test]
        public async Task Handle_UnknownToken_ReturnsFailure()
        {
            var response = await _handler.Handle(new GetPublicDischargeSummaryPdfRequestModel { AccessToken = "does-not-exist" }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
        }

        [Test]
        public async Task Handle_SignedButNoPdfUploadedYet_ReturnsFailure()
        {
            SeedSummary(isSigned: true, pdfBlobKey: null);

            var response = await _handler.Handle(new GetPublicDischargeSummaryPdfRequestModel { AccessToken = "tok123" }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
        }
    }
}
