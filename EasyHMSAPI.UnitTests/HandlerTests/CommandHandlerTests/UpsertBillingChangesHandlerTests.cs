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
    public class UpsertBillingChangesHandlerTests
    {
        private AppDbContext _context = null!;
        private UpsertBillingChangesHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new UpsertBillingChangesHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_NewChange_InsertsRecord()
        {
            // Arrange
            var hospitalId = Guid.NewGuid();
            var hospital = new Hospital { HospitalID = hospitalId, Name = "Hosp", Email = "e@m.com", Type = "General", RegistrationNumber = "REG001", Contact = "1234567890", Location = "Test Location", City = "Test City", State = "Test State", Country = "Test Country", Pincode = "123456", CreatedByUserID = Guid.NewGuid()  };
            _context.Hospitals.Add(hospital);
            await _context.SaveChangesAsync();

            var request = new UpsertBillingChangesRequestModel
            {
                HospitalId = hospitalId,
                ChargeItemId = null,
                DisplayName = "Consultation",
                DefaultRate = 500,
                DefaultQty = 1
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.ChargeItemId, Is.Not.Null);
            
            var item = await _context.BillingChargeCatalog.FirstOrDefaultAsync(x => x.HospitalId == hospitalId);
            Assert.That(item, Is.Not.Null);
            Assert.That(item!.DisplayName, Is.EqualTo("Consultation"));
        }

        [Test]
        public async Task Handle_ExistingChange_UpdatesRecord()
        {
             // Arrange
            var hospitalId = Guid.NewGuid();
            var hospital = new Hospital { HospitalID = hospitalId, Name = "Hosp", Email = "e@m.com", Type = "General", RegistrationNumber = "REG001", Contact = "1234567890", Location = "Test Location", City = "Test City", State = "Test State", Country = "Test Country", Pincode = "123456", CreatedByUserID = Guid.NewGuid()  };
            _context.Hospitals.Add(hospital);
            
            var chargeId = Guid.NewGuid();
            var item = new BillingChargeCatalog
            {
                ChargeItemId = chargeId,
                HospitalId = hospitalId,
                DisplayName = "Old Name",
                DefaultRate = 400
            };
            _context.BillingChargeCatalog.Add(item);
            await _context.SaveChangesAsync();

            var request = new UpsertBillingChangesRequestModel
            {
                HospitalId = hospitalId,
                ChargeItemId = chargeId,
                DisplayName = "New Name",
                DefaultRate = 600
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            
            var updated = await _context.BillingChargeCatalog.FindAsync(chargeId);
            Assert.That(updated!.DisplayName, Is.EqualTo("New Name"));
            Assert.That(updated.DefaultRate, Is.EqualTo(600));
        }

        [Test]
        public async Task Handle_HospitalNotFound_ReturnsFailure()
        {
            // Arrange
            var request = new UpsertBillingChangesRequestModel { HospitalId = Guid.NewGuid() };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Hospital does not exist."));
        }
    }
}
