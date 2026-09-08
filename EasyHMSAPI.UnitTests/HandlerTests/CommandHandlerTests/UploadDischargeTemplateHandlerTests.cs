using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class UploadDischargeTemplateHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<IBlobStorageService> _mockBlobService = null!;
        private Mock<IConfiguration> _mockConfig = null!;
        private UploadDischargeTemplateHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _mockBlobService = new Mock<IBlobStorageService>();
            _mockConfig = new Mock<IConfiguration>();

            _mockConfig.Setup(c => c["BlobStorage:DischargeTemplatesContainer"]).Returns("dischargetemplates");

            _handler = new UploadDischargeTemplateHandler(_mockConfig.Object, _mockBlobService.Object, _context);
        }

        [TearDown]
        public void TearDown()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        private static IFormFile CreateFormFile(byte[] bytes, string fileName = "template.pdf", string contentType = "application/pdf")
        {
            var stream = new MemoryStream(bytes);
            return new FormFile(stream, 0, bytes.Length, "file", fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = contentType,
            };
        }

        [Test]
        public async Task Handle_DoctorNotFound_ReturnsError()
        {
            var request = new UploadDischargeTemplateRequestModel
            {
                DoctorId = Guid.NewGuid(),
                HospitalId = Guid.NewGuid(),
            };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Invalid doctor Id"));
        }

        [Test]
        public async Task Handle_HospitalNotFound_ReturnsError()
        {
            var doctorId = Guid.NewGuid();
            _context.Doctors.Add(TestEntityFactory.CreateDoctor(doctorId));
            await _context.SaveChangesAsync();

            var request = new UploadDischargeTemplateRequestModel
            {
                DoctorId = doctorId,
                HospitalId = Guid.NewGuid(),
            };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Invalid hospital Id"));
        }

        [Test]
        public async Task Handle_NonPdfFile_ReturnsErrorWithoutUploading()
        {
            // A file whose bytes don't start with the "%PDF-" magic header, regardless of its
            // declared content-type/filename (both are client-supplied and untrustworthy).
            var doctorId = Guid.NewGuid();
            var hospitalId = Guid.NewGuid();
            _context.Doctors.Add(TestEntityFactory.CreateDoctor(doctorId));
            _context.Hospitals.Add(TestEntityFactory.CreateHospital(hospitalId));
            await _context.SaveChangesAsync();

            var request = new UploadDischargeTemplateRequestModel
            {
                DoctorId = doctorId,
                HospitalId = hospitalId,
                File = CreateFormFile(Encoding.UTF8.GetBytes("this is not a pdf")),
                LoggedInUserId = Guid.NewGuid(),
            };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("The uploaded file is not a valid PDF."));
            _mockBlobService.Verify(
                x => x.UploadAsync(It.IsAny<string>(), It.IsAny<IFormFile>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Test]
        public async Task Handle_ValidPdfFile_UploadsSuccessfully()
        {
            var doctorId = Guid.NewGuid();
            var hospitalId = Guid.NewGuid();
            var uploadedUrl = "http://blob.example/discharge-template.pdf";

            _context.Doctors.Add(TestEntityFactory.CreateDoctor(doctorId));
            _context.Hospitals.Add(TestEntityFactory.CreateHospital(hospitalId));
            // Pre-seed the settings row (with a RowVersion set) so the handler takes its
            // update-existing path rather than create-new -- EF Core's InMemory provider, unlike
            // real SQL Server, doesn't auto-generate [Timestamp] columns on insert, so a freshly
            // created DischargeSetting with no RowVersion set (exactly what the handler's own
            // create-new branch does, correctly, for the real database) fails against InMemory
            // with "Required properties '{RowVersion}' are missing". Not a bug in the handler.
            _context.DischargeSettings.Add(TestEntityFactory.CreateDischargeSetting(hospitalId, doctorId));
            await _context.SaveChangesAsync();

            _mockBlobService.Setup(x => x.UploadAsync(
                It.IsAny<string>(),
                It.IsAny<IFormFile>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(uploadedUrl);

            var request = new UploadDischargeTemplateRequestModel
            {
                DoctorId = doctorId,
                HospitalId = hospitalId,
                File = CreateFormFile(Encoding.ASCII.GetBytes("%PDF-1.4\n%mock pdf content for testing")),
                LoggedInUserId = Guid.NewGuid(),
            };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.Url, Is.EqualTo(uploadedUrl));
            Assert.That(response.Message, Is.EqualTo("Discharge letterhead template uploaded successfully."));

            var settings = await _context.DischargeSettings.FirstOrDefaultAsync(ds => ds.DoctorId == doctorId);
            Assert.That(settings, Is.Not.Null);
            Assert.That(settings!.URI, Is.EqualTo(uploadedUrl));
        }
    }
}
