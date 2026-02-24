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
    public class UploadInvoiceSettingsTemplateHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<IBlobStorageService> _blobStorageServiceMock = null!;
        private Mock<IConfiguration> _configurationMock = null!;
        private UploadInvoiceSettingsTemplateHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _blobStorageServiceMock = new Mock<IBlobStorageService>();
            _configurationMock = new Mock<IConfiguration>();
            
            _configurationMock.SetupGet(x => x["BlobStorage:InvoiceTemplatesContainer"]).Returns("invoices");

            _handler = new UploadInvoiceSettingsTemplateHandler(
                _configurationMock.Object,
                _blobStorageServiceMock.Object, 
                _context);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ValidRequest_UploadsTemplate()
        {
            // Arrange
            var hospitalId = Guid.NewGuid();
            var hospital = new Hospital { HospitalID = hospitalId, Name = "Hosp", Email = "e@m.com", Type = "General", RegistrationNumber = "REG001", Contact = "1234567890", Location = "Test Location", City = "Test City", State = "Test State", Country = "Test Country", Pincode = "123456", CreatedByUserID = Guid.NewGuid()  };
            _context.Hospitals.Add(hospital);
            await _context.SaveChangesAsync();

            var fileMock = new Mock<IFormFile>();
            _blobStorageServiceMock.Setup(x => x.UploadAsync(It.IsAny<string>(), It.IsAny<IFormFile>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("http://blob.url/template.pdf");

            var request = new UploadInvoiceSettingsTemplateRequestModel
            {
                HospitalId = hospitalId,
                TemplateFile = fileMock.Object,
                LoggedInUserId = Guid.NewGuid()
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            
            var setting = await _context.InvoicePrintSettings.FirstOrDefaultAsync(s => s.HospitalId == hospitalId);
            Assert.That(setting, Is.Not.Null);
            Assert.That(setting!.URI, Is.EqualTo("http://blob.url/template.pdf"));
        }

        [Test]
        public async Task Handle_HospitalNotFound_ReturnsFailure()
        {
             // Arrange
            var request = new UploadInvoiceSettingsTemplateRequestModel { HospitalId = Guid.NewGuid() };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Invalid HospitalId."));
        }
    }
}
