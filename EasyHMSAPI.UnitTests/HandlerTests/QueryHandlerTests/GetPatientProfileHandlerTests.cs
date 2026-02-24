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
    public class GetPatientProfileHandlerTests
    {
        private AppDbContext _context = null!;
        private GetPatientProfileHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetPatientProfileHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ReturnsProfile()
        {
            // Arrange
            var hospitalId = Guid.NewGuid();
            var user = TestDataFactory.SeedUser(_context);
            var patientId = "PAT1";
            var patient = new PatientRegistration 
            { 
                PatientId = patientId, 
                HospitalId = hospitalId, 
                FullName = "John Doe",
                Mobile = "1234567890"
            };
            _context.PatientRegistrations.Add(patient);
            await _context.SaveChangesAsync();

             var request = new GetPatientProfileRequestModel
            {
                HospitalId = hospitalId,
                PatientId = patientId
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response, Is.Not.Null);
            Assert.That(response!.FullName, Is.EqualTo("John Doe"));
            Assert.That(response.Mobile, Is.EqualTo("1234567890"));
        }

         [Test]
        public async Task Handle_NotFound_ReturnsNull()
        {
            // Arrange
             var request = new GetPatientProfileRequestModel 
            { 
                HospitalId = Guid.NewGuid(), 
                PatientId = "PAT1" 
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response, Is.Null);
        }
    }
}
