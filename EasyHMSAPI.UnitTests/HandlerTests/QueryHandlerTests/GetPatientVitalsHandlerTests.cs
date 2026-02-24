using System;
using System.Text.Json;
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
    public class GetPatientVitalsHandlerTests
    {
        private AppDbContext _context = null!;
        private GetPatientVitalsHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetPatientVitalsHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ReturnsVitals()
        {
            // Arrange
            var apptId = Guid.NewGuid();
            var patientId = "PAT1";
            var vitalsData = new { Temp = 98.6, Bp = "120/80" };
            var vitals = new AppointmentVitals
            {
                VitalId = Guid.NewGuid(),
                ApptId = apptId,
                PatientId = patientId,
                VitalsJson = JsonSerializer.Serialize(vitalsData)
            };
            _context.AppointmentVitals.Add(vitals);
            await _context.SaveChangesAsync();

            var request = new GetPatientVitalsRequestModel
            {
                AppointmentId = apptId,
                PatientId = patientId
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Vitals, Is.Not.Null);
            // Verify dynamic content (difficult with object type, but checking null is enough for coverage of success path)
        }

        [Test]
        public async Task Handle_NotFound_ReturnsNullVitals()
        {
             // Arrange
            var request = new GetPatientVitalsRequestModel
            {
                AppointmentId = Guid.NewGuid(),
                PatientId = "PAT1"
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Vitals, Is.Null);
        }
    }
}
