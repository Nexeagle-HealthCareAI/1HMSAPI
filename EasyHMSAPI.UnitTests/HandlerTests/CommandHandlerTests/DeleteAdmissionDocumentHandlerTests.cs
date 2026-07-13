using System;
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
    public class DeleteAdmissionDocumentHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<IBlobStorageService> _blobStorageServiceMock = null!;
        private Mock<IConfiguration> _configurationMock = null!;
        private DeleteAdmissionDocumentHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _blobStorageServiceMock = new Mock<IBlobStorageService>();
            _configurationMock = new Mock<IConfiguration>();
            _configurationMock.SetupGet(x => x["BlobStorage:AdmissionDocumentsContainer"]).Returns("admissiondocuments");

            _handler = new DeleteAdmissionDocumentHandler(_context, _blobStorageServiceMock.Object, _configurationMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private (Guid hospitalId, Admission admission, AdmissionDocument document) SeedBasics()
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
                AdmissionId = Guid.NewGuid(), HospitalId = hospitalId, PatientId = "PTID00000001",
                AdmissionNo = "ADM-1", AdmittedAt = DateTime.UtcNow, StatusCode = "ADMITTED",
            };
            _context.Admission.Add(admission);

            var document = new AdmissionDocument
            {
                DocumentId = Guid.NewGuid(), HospitalId = hospitalId, AdmissionId = admission.AdmissionId,
                DocumentName = "report.pdf", ContentType = "application/pdf", FileSizeBytes = 100,
                StorageObjectKey = "blob_admissiondocuments.pdf", StorageUrl = "http://minio/blob_admissiondocuments.pdf",
                UploadedAt = DateTime.UtcNow, UploadedBy = "Front Desk",
            };
            _context.AdmissionDocument.Add(document);
            _context.SaveChanges();

            return (hospitalId, admission, document);
        }

        [Test]
        public async Task Handle_ValidRequest_DeletesFromStorageAndDb()
        {
            var (hospitalId, admission, document) = SeedBasics();
            _blobStorageServiceMock.Setup(x => x.DeleteAsync(document.DocumentId.ToString(), "admissiondocuments", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var response = await _handler.Handle(new DeleteAdmissionDocumentRequestModel
            {
                HospitalId = hospitalId, AdmissionId = admission.AdmissionId, DocumentId = document.DocumentId,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            Assert.That(_context.AdmissionDocument.Find(document.DocumentId), Is.Null);
        }

        [Test]
        public async Task Handle_WrongHospital_ReturnsFailure_AndDoesNotDelete()
        {
            var (_, admission, document) = SeedBasics();

            var response = await _handler.Handle(new DeleteAdmissionDocumentRequestModel
            {
                HospitalId = Guid.NewGuid(), AdmissionId = admission.AdmissionId, DocumentId = document.DocumentId,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("not found"));
            Assert.That(_context.AdmissionDocument.Find(document.DocumentId), Is.Not.Null);
            _blobStorageServiceMock.Verify(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task Handle_UnknownDocumentId_ReturnsFailure()
        {
            var (hospitalId, admission, _) = SeedBasics();

            var response = await _handler.Handle(new DeleteAdmissionDocumentRequestModel
            {
                HospitalId = hospitalId, AdmissionId = admission.AdmissionId, DocumentId = Guid.NewGuid(),
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("not found"));
        }

        [Test]
        public async Task Handle_StorageDeleteFails_ReturnsFailure_AndKeepsDbRow()
        {
            var (hospitalId, admission, document) = SeedBasics();
            _blobStorageServiceMock.Setup(x => x.DeleteAsync(document.DocumentId.ToString(), "admissiondocuments", It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var response = await _handler.Handle(new DeleteAdmissionDocumentRequestModel
            {
                HospitalId = hospitalId, AdmissionId = admission.AdmissionId, DocumentId = document.DocumentId,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(_context.AdmissionDocument.Find(document.DocumentId), Is.Not.Null);
        }
    }
}
