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
    public class SearchPatientHandlerTests
    {
         private AppDbContext _context = null!;
        private SearchPatientHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new SearchPatientHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_SearchByMobile_ReturnsPatient()
        {
            // Arrange
            var hospitalId = Guid.NewGuid();
            var patient = new PatientRegistration 
            { 
                PatientId = "PAT1", 
                HospitalId = hospitalId, 
                FullName = "John", 
                Mobile = "1234567890" 
            };
            _context.PatientRegistrations.Add(patient);
            await _context.SaveChangesAsync();

            var request = new SearchPatientRequestModel
            {
                HospitalId = hospitalId,
                SearchText = "12345"
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Items, Has.Count.EqualTo(1));
            Assert.That(response.Items[0].FullName, Is.EqualTo("John"));
        }
    }
}
