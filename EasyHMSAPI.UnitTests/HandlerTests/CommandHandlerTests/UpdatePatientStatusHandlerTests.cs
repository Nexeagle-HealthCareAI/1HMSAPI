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
    public class UpdatePatientStatusHandlerTests
    {
        private AppDbContext _context = null!;
        private UpdatePatientStatusHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new UpdatePatientStatusHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ValidRequest_UpdatesStatus()
        {
            // Arrange
            var apptId = Guid.NewGuid();
            var patientId = "PAT123";
            var appointment = new Appointment { ApptId = apptId, PatientId = patientId, CurrentStatusCode = "Waiting" };
            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            var request = new UpdatePatientStatusRequestModel
            {
                AppointmentId = apptId,
                PatientId = patientId,
                CurrentStatus = "Waiting",
                ToStatus = "InConsult",
                UserId = Guid.NewGuid()
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.NewStatus, Is.EqualTo("InConsult"));
            
            var updated = await _context.Appointments.FindAsync(apptId);
            Assert.That(updated!.CurrentStatusCode, Is.EqualTo("InConsult"));
        }

        [Test]
        public async Task Handle_StatusMismatch_ReturnsFailure()
        {
            // Arrange
            var apptId = Guid.NewGuid();
            var appointment = new Appointment { ApptId = apptId, PatientId = "PAT123", CurrentStatusCode = "Waiting" };
            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            var request = new UpdatePatientStatusRequestModel
            {
                AppointmentId = apptId,
                PatientId = "PAT123",
                CurrentStatus = "WrongStatus",
                ToStatus = "InConsult",
                UserId = Guid.NewGuid()
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("Current status does not match"));
        }
    }
}
