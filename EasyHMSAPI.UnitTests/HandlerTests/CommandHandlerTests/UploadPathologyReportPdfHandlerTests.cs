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
        private Mock<IWhatsAppMessagingService> _whatsAppMessagingServiceMock = null!;
        private Mock<IConfiguration> _configurationMock = null!;
        private UploadPathologyReportPdfHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _blobStorageServiceMock = new Mock<IBlobStorageService>();
            _whatsAppMessagingServiceMock = new Mock<IWhatsAppMessagingService>();
            _configurationMock = new Mock<IConfiguration>();
            _configurationMock.Setup(x => x["BlobStorage:PathologyReportsContainer"]).Returns("pathology-reports");
            _handler = new UploadPathologyReportPdfHandler(
                _configurationMock.Object, _blobStorageServiceMock.Object, _whatsAppMessagingServiceMock.Object, _context);
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

        private PathologyReport SeedReport(Guid hospitalId, string? patientId = null)
        {
            var orderId = Guid.NewGuid();
            var report = new PathologyReport
            {
                ReportId = Guid.NewGuid(),
                HospitalId = hospitalId,
                OrderId = orderId,
                ReportNo = "LR-1",
                Status = "GENERATED",
            };
            _context.PathologyReport.Add(report);
            _context.PathologyOrder.Add(new PathologyOrder
            {
                OrderId = orderId,
                HospitalId = hospitalId,
                PatientId = patientId ?? "PTID-NONE",
                OrderNo = "ORD-1",
                Status = "COMPLETED",
            });
            _context.Hospitals.Add(new Hospital
            {
                HospitalID = hospitalId,
                Name = "City Care Hospital",
                Type = "GENERAL",
                RegistrationNumber = "REG-1",
                Contact = "9999999999",
                Location = "Test Location",
                City = "Test City",
                State = "Test State",
                Country = "India",
                Pincode = "000000",
            });
            _context.SaveChanges();
            return report;
        }

        [Test]
        public async Task Handle_ApprovedReportWithFile_ComputesCorrectHashAndPersists()
        {
            var hospitalId = Guid.NewGuid();
            var report = SeedReport(hospitalId);
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
        public async Task Handle_ReportNotFound_ReturnsFailureWithoutUploading()
        {
            var response = await _handler.Handle(new UploadPathologyReportPdfRequestModel
            {
                HospitalId = Guid.NewGuid(),
                ReportId = Guid.NewGuid(),
                File = FileMockWithContent(Encoding.UTF8.GetBytes("x")).Object,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            _blobStorageServiceMock.Verify(x => x.UploadAsync(It.IsAny<string>(), It.IsAny<IFormFile>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // There is no separate approval gate anymore -- generation is a single, freely-repeatable
        // step (GeneratePathologyReportHandler), so any report that exists can have its PDF
        // (re-)uploaded regardless of its Status value.
        [Test]
        public async Task Handle_ReportExistsRegardlessOfStatus_UploadsSuccessfully()
        {
            var hospitalId = Guid.NewGuid();
            var report = SeedReport(hospitalId);

            _blobStorageServiceMock
                .Setup(x => x.UploadAsync(It.IsAny<string>(), It.IsAny<IFormFile>(), "pathology-reports", It.IsAny<CancellationToken>()))
                .ReturnsAsync("https://blob.example/report.pdf");

            var response = await _handler.Handle(new UploadPathologyReportPdfRequestModel
            {
                HospitalId = hospitalId,
                ReportId = report.ReportId,
                File = FileMockWithContent(Encoding.UTF8.GetBytes("x")).Object,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
        }

        [Test]
        public async Task Handle_NoFile_ReturnsFailure()
        {
            var hospitalId = Guid.NewGuid();
            var report = SeedReport(hospitalId);

            var response = await _handler.Handle(new UploadPathologyReportPdfRequestModel
            {
                HospitalId = hospitalId,
                ReportId = report.ReportId,
                File = null,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
        }

        [Test]
        public async Task Handle_PatientHasMobileOnFile_DispatchesWhatsAppLabReport()
        {
            var hospitalId = Guid.NewGuid();
            var patientId = "PTID-1";
            var report = SeedReport(hospitalId, patientId);
            _context.PatientRegistrations.Add(new PatientRegistration
            {
                PatientId = patientId,
                HospitalId = hospitalId,
                FullName = "Amir Yadav",
                Mobile = "9999912345",
            });
            _context.SaveChanges();

            _blobStorageServiceMock
                .Setup(x => x.UploadAsync(It.IsAny<string>(), It.IsAny<IFormFile>(), "pathology-reports", It.IsAny<CancellationToken>()))
                .ReturnsAsync("https://blob.example/report.pdf");

            await _handler.Handle(new UploadPathologyReportPdfRequestModel
            {
                HospitalId = hospitalId,
                ReportId = report.ReportId,
                File = FileMockWithContent(Encoding.UTF8.GetBytes("x")).Object,
            }, CancellationToken.None);

            _whatsAppMessagingServiceMock.Verify(x => x.SendLabReportAsync(
                "9999912345", "https://blob.example/report.pdf", "LR-1.pdf", "City Care Hospital", "Amir Yadav"), Times.Once);
        }

        [Test]
        public async Task Handle_PatientHasNoMobileOnFile_StillSucceedsWithoutDispatching()
        {
            var hospitalId = Guid.NewGuid();
            var report = SeedReport(hospitalId); // patientId defaults to one with no PatientRegistration row

            _blobStorageServiceMock
                .Setup(x => x.UploadAsync(It.IsAny<string>(), It.IsAny<IFormFile>(), "pathology-reports", It.IsAny<CancellationToken>()))
                .ReturnsAsync("https://blob.example/report.pdf");

            var response = await _handler.Handle(new UploadPathologyReportPdfRequestModel
            {
                HospitalId = hospitalId,
                ReportId = report.ReportId,
                File = FileMockWithContent(Encoding.UTF8.GetBytes("x")).Object,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            _whatsAppMessagingServiceMock.Verify(x => x.SendLabReportAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }
    }
}
