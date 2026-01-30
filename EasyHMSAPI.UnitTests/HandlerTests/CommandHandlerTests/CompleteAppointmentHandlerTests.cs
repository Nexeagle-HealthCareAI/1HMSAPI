using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class CompleteAppointmentHandlerTests
    {
        private AppDbContext _context = null!;
        private CompleteAppointmentHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new CompleteAppointmentHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ValidRequest_CompletesAppointment()
        {
            // Arrange
            var hospitalId = Guid.NewGuid();
            var user = TestDataFactory.SeedUser(_context);
            var patientId = "PAT123";
            var doctorId = Guid.NewGuid();

            var appointment = new Appointment
            {
                ApptId = Guid.NewGuid(),
                DoctorId = doctorId,
                HospitalId = hospitalId,
                PatientId = patientId,
                CurrentStatusCode = "Booked"
            };
            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            var request = new CompleteAppointmentRequestModel
            {
                AppointmentId = appointment.ApptId,
                DoctordId = doctorId,
                HospitalId = hospitalId,
                PatientId = patientId
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            var updatedAppt = await _context.Appointments.FindAsync(appointment.ApptId);
            Assert.That(updatedAppt.CurrentStatusCode, Is.EqualTo(AppConstants.AppointmentStatus_Completed));
        }

        [Test]
        public async Task Handle_AppointmentNotFound_ReturnsFailure()
        {
            // Arrange
            var request = new CompleteAppointmentRequestModel
            {
                 AppointmentId = Guid.NewGuid(),
                 PatientId = "nonexistent"
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Appointment not found."));
        }
    }
}
