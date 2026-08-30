using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class UploadPathologyReportPdfHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<IBlobStorageService> _blobStorageServiceMock = null!;
        private Mock<IConfiguration> _configurationMock = null!;
        private UploadPathologyReportPdfHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _blobStorageServiceMock = new Mock<IBlobStorageService>();
            _configurationMock = new Mock<IConfiguration>();
            _configurationMock.Setup(x => x["BlobStorage:PathologyReportsContainer"]).Returns("pathology-reports");
            _handler = new UploadPathologyReportPdfHandler(_configurationMock.Object, _blobStorageServiceMock.Object, _context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        private static Mock<IFormFile> FileMockWithContent(byte[] bytes)
        {
            var fileMock = new Mock<IFormFile>();
            fileMock.SetupGet(f => f.Length).Returns(bytes.Length);
            fileMock.Setup(f => f.OpenReadStream()).Returns(() => new MemoryStream(bytes));
            return fileMock;
        }

        private PathologyReport SeedApprovedReport(Guid hospitalId)
        {
            var report = new PathologyReport
            {
                ReportId = Guid.NewGuid(),
                HospitalId = hospitalId,
                OrderId = Guid.NewGuid(),
                ReportNo = "LR-1",
                Status = "APPROVED",
            };
            _context.PathologyReport.Add(report);
            _context.SaveChanges();
            return report;
        }

        [Test]
        public async Task Handle_ApprovedReportWithFile_ComputesCorrectHashAndPersists()
        {
            var hospitalId = Guid.NewGuid();
            var report = SeedApprovedReport(hospitalId);
            var bytes = Encoding.UTF8.GetBytes("%PDF-1.4 fake pdf content for hashing");
            var expectedHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

            _blobStorageServiceMock
                .Setup(x => x.UploadAsync(report.ReportId.ToString(), It.IsAny<IFormFile>(), "pathology-reports", It.IsAny<CancellationToken>()))
                .ReturnsAsync("https://blob.example/pathology-reports/report.pdf");

            var response = await _handler.Handle(new UploadPathologyReportPdfRequestModel
            {
                HospitalId = hospitalId,
                ReportId = report.ReportId,
                File = FileMockWithContent(bytes).Object,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            Assert.That(response.Sha256, Is.EqualTo(expectedHash));
            var saved = _context.PathologyReport.Single(r => r.ReportId == report.ReportId);
            Assert.That(saved.PdfSha256, Is.EqualTo(expectedHash));
            Assert.That(saved.PdfBlobPath, Is.EqualTo("https://blob.example/pathology-reports/report.pdf"));
        }

        [Test]
        public async Task Handle_ReportNotYetApproved_ReturnsFailureWithoutUploading()
        {
            var hospitalId = Guid.NewGuid();
            var report = new PathologyReport
            {
                ReportId = Guid.NewGuid(),
                HospitalId = hospitalId,
                OrderId = Guid.NewGuid(),
                ReportNo = "LR-2",
                Status = "TECH_SIGNED",
            };
            _context.PathologyReport.Add(report);
            _context.SaveChanges();

            var response = await _handler.Handle(new UploadPathologyReportPdfRequestModel
            {
                HospitalId = hospitalId,
                ReportId = report.ReportId,
                File = FileMockWithContent(Encoding.UTF8.GetBytes("x")).Object,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            _blobStorageServiceMock.Verify(x => x.UploadAsync(It.IsAny<string>(), It.IsAny<IFormFile>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task Handle_NoFile_ReturnsFailure()
        {
            var hospitalId = Guid.NewGuid();
            var report = SeedApprovedReport(hospitalId);

            var response = await _handler.Handle(new UploadPathologyReportPdfRequestModel
            {
                HospitalId = hospitalId,
                ReportId = report.ReportId,
                File = null,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
        }
    }
}
