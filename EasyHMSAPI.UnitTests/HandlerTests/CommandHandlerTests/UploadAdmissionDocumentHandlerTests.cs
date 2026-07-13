using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class UploadAdmissionDocumentHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<IBlobStorageService> _blobStorageServiceMock = null!;
        private Mock<IConfiguration> _configurationMock = null!;
        private UploadAdmissionDocumentHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _blobStorageServiceMock = new Mock<IBlobStorageService>();
            _configurationMock = new Mock<IConfiguration>();
            _configurationMock.SetupGet(x => x["BlobStorage:AdmissionDocumentsContainer"]).Returns("admissiondocuments");

            _handler = new UploadAdmissionDocumentHandler(_context, _blobStorageServiceMock.Object, _configurationMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private (Guid hospitalId, Admission admission) SeedBasics()
        {
            var hospitalId = Guid.NewGuid();
            _context.Hospitals.Add(new Hospital
            {
                HospitalID = hospitalId, Name = "Hosp", Email = "e@m.com", Type = "General", RegistrationNumber = "REG001",
                Contact = "1234567890", Location = "Test Location", City = "Test City", State = "Test State",
                Country = "Test Country", Pincode = "123456", CreatedByUserID = Guid.NewGuid(),
            });

            var admission = new Admission
            {
                AdmissionId = Guid.NewGuid(),
                HospitalId = hospitalId,
                PatientId = "PTID00000001",
                AdmissionNo = "ADM-1",
                AdmittedAt = DateTime.UtcNow,
                StatusCode = "ADMITTED",
            };
            _context.Admission.Add(admission);
            _context.SaveChanges();

            return (hospitalId, admission);
        }

        private static Mock<IFormFile> ValidFileMock(string fileName = "report.pdf", long length = 1024, string contentType = "application/pdf")
        {
            var fileMock = new Mock<IFormFile>();
            fileMock.SetupGet(f => f.FileName).Returns(fileName);
            fileMock.SetupGet(f => f.Length).Returns(length);
            fileMock.SetupGet(f => f.ContentType).Returns(contentType);
            return fileMock;
        }

        [Test]
        public async Task Handle_ValidRequest_UploadsAndPersistsDocument()
        {
            var (hospitalId, admission) = SeedBasics();
            var fileMock = ValidFileMock();
            _blobStorageServiceMock.Setup(x => x.UploadAsync(It.IsAny<string>(), It.IsAny<IFormFile>(), "admissiondocuments", It.IsAny<CancellationToken>()))
                .ReturnsAsync("blob123_admissiondocuments.pdf|http://minio/blob123_admissiondocuments.pdf");

            var response = await _handler.Handle(new UploadAdmissionDocumentRequestModel
            {
                HospitalId = hospitalId, AdmissionId = admission.AdmissionId, File = fileMock.Object, UploadedByUserName = "Front Desk",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            Assert.That(response.DocumentId, Is.Not.Null);
            Assert.That(response.FileUrl, Is.EqualTo("http://minio/blob123_admissiondocuments.pdf"));

            var saved = await _context.AdmissionDocument.FirstOrDefaultAsync(d => d.DocumentId == response.DocumentId);
            Assert.That(saved, Is.Not.Null);
            Assert.That(saved!.DocumentName, Is.EqualTo("report.pdf"));
            Assert.That(saved.UploadedBy, Is.EqualTo("Front Desk"));
            Assert.That(saved.StorageObjectKey, Is.EqualTo("blob123_admissiondocuments.pdf"));
        }

        [Test]
        public async Task Handle_MissingFile_ReturnsFailure()
        {
            var (hospitalId, admission) = SeedBasics();

            var response = await _handler.Handle(new UploadAdmissionDocumentRequestModel
            {
                HospitalId = hospitalId, AdmissionId = admission.AdmissionId, File = null,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("required"));
        }

        [Test]
        public async Task Handle_OversizeFile_ReturnsFailure()
        {
            var (hospitalId, admission) = SeedBasics();
            var fileMock = ValidFileMock(length: 21 * 1024 * 1024);

            var response = await _handler.Handle(new UploadAdmissionDocumentRequestModel
            {
                HospitalId = hospitalId, AdmissionId = admission.AdmissionId, File = fileMock.Object,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("too large"));
        }

        [Test]
        public async Task Handle_DisallowedExtension_ReturnsFailure()
        {
            var (hospitalId, admission) = SeedBasics();
            var fileMock = ValidFileMock(fileName: "malware.exe");

            var response = await _handler.Handle(new UploadAdmissionDocumentRequestModel
            {
                HospitalId = hospitalId, AdmissionId = admission.AdmissionId, File = fileMock.Object,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("Unsupported"));
        }

        [Test]
        public async Task Handle_UnknownAdmission_ReturnsFailure()
        {
            var (hospitalId, _) = SeedBasics();
            var fileMock = ValidFileMock();

            var response = await _handler.Handle(new UploadAdmissionDocumentRequestModel
            {
                HospitalId = hospitalId, AdmissionId = Guid.NewGuid(), File = fileMock.Object,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("not found"));
        }
    }
}
