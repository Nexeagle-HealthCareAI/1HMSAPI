using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class UpsertInvoiceSettingsHandlerTests
    {
        private AppDbContext _context = null!;
        private UpsertInvoiceSettingsHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new UpsertInvoiceSettingsHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_NewSettings_InsertsRecord()
        {
            // Arrange
            var hospitalId = Guid.NewGuid();
            var request = new UpsertInvoiceSettingsRequestModel
            {
                HospitalId = hospitalId,
                InvoicePrintId = Guid.NewGuid(),
                FeaderHeight = 50,
                FooterHeight = 50
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.InvoicePrintId, Is.Not.Null);
            
            var setting = await _context.InvoicePrintSettings.FirstOrDefaultAsync(s => s.HospitalId == hospitalId);
            Assert.That(setting, Is.Not.Null);
            Assert.That(setting!.HeaderHeight, Is.EqualTo(50));
        }

        [Test]
        public async Task Handle_ExistingSettings_UpdatesRecord()
        {
             // Arrange
            var hospitalId = Guid.NewGuid();
            var invoicePrintId = Guid.NewGuid();
            var setting = new InvoicePrintSettings
            {
                InvoicePrintId = invoicePrintId,
                HospitalId = hospitalId,
                HeaderHeight = 40
            };
            _context.InvoicePrintSettings.Add(setting);
            await _context.SaveChangesAsync();

            var request = new UpsertInvoiceSettingsRequestModel
            {
                HospitalId = hospitalId,
                InvoicePrintId = invoicePrintId,
                FeaderHeight = 60
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            
            var updated = await _context.InvoicePrintSettings.FindAsync(invoicePrintId);
            Assert.That(updated!.HeaderHeight, Is.EqualTo(600 * 0.1)); // Wait, 60? The code just assigns it. Let's check logic.
            // Code: existingSettings.HeaderHeight = request.FeaderHeight;
            // It seems straightforward.
            Assert.That(updated.HeaderHeight, Is.EqualTo(60));
        }
    }
}
