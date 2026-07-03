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
    public class UpdatePatientProfileHandlerTests
    {
        private AppDbContext _context = null!;
        private UpdatePatientProfileHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new UpdatePatientProfileHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ValidRequest_UpdatesProfile()
        {
            // Arrange
            var hospitalId = Guid.NewGuid();
            var user = TestDataFactory.SeedUser(_context);
            var patientId = "PAT123";
            var patient = new PatientRegistration 
            { 
                PatientId = patientId, 
                HospitalId = hospitalId, 
                FullName = "Old Name" 
            };
            _context.PatientRegistrations.Add(patient);
            await _context.SaveChangesAsync();

            var request = new UpdatePatientProfileRequestModel
            {
                PatientId = patientId,
                HospitalId = hospitalId,
                FullName = "New Name",
                Age = 30
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            
            var updated = await _context.PatientRegistrations.FirstOrDefaultAsync(p => p.PatientId == patientId);
            Assert.That(updated!.FullName, Is.EqualTo("New Name"));
            Assert.That(updated.Age, Is.EqualTo(30));
        }

        [Test]
        public async Task Handle_PatientNotFound_ReturnsFailure()
        {
            // Arrange
            var request = new UpdatePatientProfileRequestModel
            {
                PatientId = "UNKNOWN",
                HospitalId = Guid.NewGuid()
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Patient not found."));
        }
    }
}
