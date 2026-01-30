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
    public class HospitalKpiMatrixHandlerTests
    {
         private AppDbContext _context = null!;
        private HospitalKpiMatrixHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new HospitalKpiMatrixHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ReturnsKpis()
        {
            // Arrange
            var hospitalId = Guid.NewGuid();
            var status = new StatusMaster { StatusCode = "Completed", DisplayName = "Completed" };
            _context.StatusMasters.Add(status);
           
            var appointment = new Appointment
            {
                ApptId = Guid.NewGuid(),
                HospitalId = hospitalId,
                CurrentStatusCode = "Completed",
                ApptDate = DateTime.Today
            };
            _context.Appointments.Add(appointment);
             await _context.SaveChangesAsync();

            var request = new HospitalKpiMatrixRequestModel { HospitalId = hospitalId };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.StatusKpis, Has.Count.EqualTo(1));
            Assert.That(response.StatusKpis[0].PatientCount, Is.EqualTo(1));
        }
    }
}
