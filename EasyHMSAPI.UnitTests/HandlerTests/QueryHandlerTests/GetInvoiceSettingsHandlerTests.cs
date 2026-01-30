using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class GetInvoiceSettingsHandlerTests
    {
        private AppDbContext _context = null!;
        private GetInvoiceSettingsHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetInvoiceSettingsHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ReturnsSettings()
        {
            // Arrange
            var hospitalId = Guid.NewGuid();
            var hospital = new Hospital { HospitalID = hospitalId, Name = "Hosp", Type = "General", RegistrationNumber = "REG001", Contact = "1234567890", Location = "Test Location", City = "Test City", State = "Test State", Country = "Test Country", Pincode = "123456", CreatedByUserID = Guid.NewGuid()  };
            _context.Hospitals.Add(hospital);

            var settings = new InvoicePrintSettings
            {
                InvoicePrintId = Guid.NewGuid(),
                HospitalId = hospitalId,
                HeaderHeight = 100
            };
            _context.InvoicePrintSettings.Add(settings);
            await _context.SaveChangesAsync();

            var request = new GetInvoiceSettingsRequestModel { HospitalId = hospitalId };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.InvoiceSettings, Is.Not.Null);
            Assert.That(response.InvoiceSettings!.HeaderHeight, Is.EqualTo(100));
        }

         [Test]
        public async Task Handle_HospitalNotFound_ReturnsFailure()
        {
            // Arrange
            var request = new GetInvoiceSettingsRequestModel { HospitalId = Guid.NewGuid() };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Invalid hospital Id"));
        }
    }
}
