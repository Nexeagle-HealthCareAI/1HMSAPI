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
    public class GetAdmissionDocumentsHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<IBlobStorageService> _blobStorageServiceMock = null!;
        private Mock<IConfiguration> _configurationMock = null!;
        private GetAdmissionDocumentsHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _blobStorageServiceMock = new Mock<IBlobStorageService>();
            _configurationMock = new Mock<IConfiguration>();
            _configurationMock.SetupGet(x => x["BlobStorage:AdmissionDocumentsContainer"]).Returns("admissiondocuments");

            // Re-signing just echoes the stored URL back — behavior under test is the handler's
            // ordering/counting/scoping, not S3StorageService's actual signing.
            _blobStorageServiceMock.Setup(x => x.RefreshUrlAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string container, string prefix, string? stored, CancellationToken ct) => stored);

            _handler = new GetAdmissionDocumentsHandler(_context, _blobStorageServiceMock.Object, _configurationMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private (Guid hospitalId, Admission admission) SeedAdmission()
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
            _context.SaveChanges();
            return (hospitalId, admission);
        }

        private AdmissionDocument SeedDocument(Guid hospitalId, Guid admissionId, string name, DateTime uploadedAt)
        {
            var doc = new AdmissionDocument
            {
                DocumentId = Guid.NewGuid(), HospitalId = hospitalId, AdmissionId = admissionId,
                DocumentName = name, ContentType = "application/pdf", FileSizeBytes = 100,
                StorageObjectKey = $"{Guid.NewGuid()}_admissiondocuments.pdf", StorageUrl = "http://minio/x.pdf",
                UploadedAt = uploadedAt, UploadedBy = "Front Desk",
            };
            _context.AdmissionDocument.Add(doc);
            _context.SaveChanges();
            return doc;
        }

        [Test]
        public async Task Handle_ReturnsDocuments_NewestFirst_WithCorrectCount()
        {
            var (hospitalId, admission) = SeedAdmission();
            SeedDocument(hospitalId, admission.AdmissionId, "old.pdf", DateTime.UtcNow.AddHours(-2));
            SeedDocument(hospitalId, admission.AdmissionId, "new.pdf", DateTime.UtcNow);

            var response = await _handler.Handle(new GetAdmissionDocumentsRequestModel { HospitalId = hospitalId, AdmissionId = admission.AdmissionId }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.DocumentCount, Is.EqualTo(2));
            Assert.That(response.Documents[0].DocumentName, Is.EqualTo("new.pdf"));
            Assert.That(response.Documents[1].DocumentName, Is.EqualTo("old.pdf"));
        }

        [Test]
        public async Task Handle_NoDocuments_ReturnsEmptyList()
        {
            var (hospitalId, admission) = SeedAdmission();

            var response = await _handler.Handle(new GetAdmissionDocumentsRequestModel { HospitalId = hospitalId, AdmissionId = admission.AdmissionId }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.DocumentCount, Is.EqualTo(0));
            Assert.That(response.Documents, Is.Empty);
        }

        [Test]
        public async Task Handle_OnlyReturnsDocumentsForTheRequestedAdmission()
        {
            var (hospitalId, admission) = SeedAdmission();
            var (_, otherAdmission) = SeedAdmission();
            SeedDocument(hospitalId, admission.AdmissionId, "mine.pdf", DateTime.UtcNow);
            SeedDocument(hospitalId, otherAdmission.AdmissionId, "theirs.pdf", DateTime.UtcNow);

            var response = await _handler.Handle(new GetAdmissionDocumentsRequestModel { HospitalId = hospitalId, AdmissionId = admission.AdmissionId }, CancellationToken.None);

            Assert.That(response.DocumentCount, Is.EqualTo(1));
            Assert.That(response.Documents[0].DocumentName, Is.EqualTo("mine.pdf"));
        }
    }
}
