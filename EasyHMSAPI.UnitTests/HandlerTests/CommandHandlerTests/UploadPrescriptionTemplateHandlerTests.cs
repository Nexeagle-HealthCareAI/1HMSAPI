using System;
using System.IO;
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
    public class UploadPrescriptionTemplateHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<IBlobStorageService> _mockBlobService = null!;
        private Mock<IConfiguration> _mockConfig = null!;
        private UploadPrescriptionTemplateHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _mockBlobService = new Mock<IBlobStorageService>();
            _mockConfig = new Mock<IConfiguration>();
            
            _mockConfig.Setup(c => c["BlobStorage:PrescriptionTemplatesContainer"]).Returns("templates");

            _handler = new UploadPrescriptionTemplateHandler(_mockConfig.Object, _mockBlobService.Object, _context);
        }

        [TearDown]
        public void TearDown()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        [Test]
        public async Task Handle_DoctorNotFound_ReturnsError()
        {
            // Arrange
            var request = new UploadPrescriptionTemplateRequestModel
            {
                DoctorId = Guid.NewGuid(),
                HospitalId = Guid.NewGuid()
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Invalid doctor Id"));
        }

        [Test]
        public async Task Handle_HospitalNotFound_ReturnsError()
        {
            // Arrange
            var doctorId = Guid.NewGuid();
            _context.Doctors.Add(TestEntityFactory.CreateDoctor(doctorId ));
            await _context.SaveChangesAsync();

            var request = new UploadPrescriptionTemplateRequestModel
            {
                DoctorId = doctorId,
                HospitalId = Guid.NewGuid()
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Invalid hospital Id"));
        }

        //[Test]
        //public async Task Handle_UploadSuccess_ReturnsUrl()
        //{
        //    // Arrange
        //    var doctorId = Guid.NewGuid();
        //    var hospitalId = Guid.NewGuid();
        //    var fileMock = new Mock<IFormFile>();
        //    var uploadedUrl = "http://blob.com/template.png";

        //    _context.Doctors.Add(TestEntityFactory.CreateDoctor(doctorId ));
        //    _context.Hospitals.Add(TestEntityFactory.CreateHospital(hospitalId ));
        //    await _context.SaveChangesAsync();

        //    _mockBlobService.Setup(x => x.UploadAsync(
        //        It.IsAny<string>(), 
        //        It.IsAny<IFormFile>(), 
        //        It.IsAny<string>(), 
        //        It.IsAny<CancellationToken>()))
        //    .ReturnsAsync(uploadedUrl);

        //    var request = new UploadPrescriptionTemplateRequestModel
        //    {
        //        DoctorId = doctorId,
        //        HospitalId = hospitalId,
        //        File = fileMock.Object,
        //        LoggedInUserId = Guid.NewGuid()
        //    };

        //    // Act
        //    var response = await _handler.Handle(request, CancellationToken.None);

        //    // Assert
        //    Assert.That(response.Success, Is.True);
        //    Assert.That(response.Url, Is.EqualTo(uploadedUrl));
        //    Assert.That(response.Message, Is.EqualTo("Prescription template uploaded successfully."));

        //    var settings = await _context.PrescriptionSettings.FirstOrDefaultAsync(ps => ps.DoctorId == doctorId);
        //    Assert.That(settings, Is.Not.Null);
        //    Assert.That(settings!.URI, Is.EqualTo(uploadedUrl));
        //}

        //[Test]
        //public async Task Handle_UploadFailure_ReturnsExceptionMessage()
        //{
        //    // Arrange
        //    var doctorId = Guid.NewGuid();
        //    var hospitalId = Guid.NewGuid();
        //    var fileMock = new Mock<IFormFile>();
            
        //    _context.Doctors.Add(TestEntityFactory.CreateDoctor(doctorId ));
        //    _context.Hospitals.Add(TestEntityFactory.CreateHospital(hospitalId ));
        //    await _context.SaveChangesAsync();

        //    _mockBlobService.Setup(x => x.UploadAsync(
        //        It.IsAny<string>(), 
        //        It.IsAny<IFormFile>(), 
        //        It.IsAny<string>(), 
        //        It.IsAny<CancellationToken>()))
        //    .ThrowsAsync(new Exception("Upload failed"));

        //    var request = new UploadPrescriptionTemplateRequestModel
        //    {
        //        DoctorId = doctorId,
        //        HospitalId = hospitalId,
        //        File = fileMock.Object,
        //        LoggedInUserId = Guid.NewGuid()
        //    };

        //    // Act
        //    var response = await _handler.Handle(request, CancellationToken.None);

        //    // Assert
        //    Assert.That(response.Success, Is.False);
        //    Assert.That(response.Message, Does.Contain("Upload failed"));
        //}
    }
}
