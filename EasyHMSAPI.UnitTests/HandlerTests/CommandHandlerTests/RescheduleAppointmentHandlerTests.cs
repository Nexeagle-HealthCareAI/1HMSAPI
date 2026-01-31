using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class RescheduleAppointmentHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<ISmsService> _smsServiceMock = null!;
        private RescheduleAppointmentHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _smsServiceMock = new Mock<ISmsService>();
            _handler = new RescheduleAppointmentHandler(_context, _smsServiceMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ValidRequest_ReschedulesAppointment()
        {
            // Arrange
            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            var appointmentId = Guid.NewGuid();
            var patientId = "PAT123";

            var appointment = new Appointment
            {
                ApptId = appointmentId,
                DoctorId = doctor.DoctorID,
                PatientId = patientId,
                ApptDate = DateTime.Today,
                StartAt = DateTime.Today.AddHours(10),
                EndAt = DateTime.Today.AddHours(10).AddMinutes(15),
                CurrentStatusCode = "Booked"
            };
            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            var newDate = DateTime.Today.AddDays(2);
            var newStartAt = newDate.AddHours(11);

            var request = new RescheduleAppointmentRequestModel
            {
                AppointmentId = appointmentId,
                PatientId = patientId,
                ToApptDate = newDate,
                ToStartAt = newStartAt
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.FinalStatus, Is.EqualTo(AppConstants.AppointmentStatus_Future));
            
            var updatedAppt = await _context.Appointments.FindAsync(appointmentId);
            Assert.That(updatedAppt!.ApptDate, Is.EqualTo(newDate));
            Assert.That(updatedAppt.StartAt, Is.EqualTo(newStartAt));
        }

        [Test]
        public async Task Handle_AppointmentNotFound_ReturnsFailure()
        {
            // Arrange
            var request = new RescheduleAppointmentRequestModel 
            { 
                AppointmentId = Guid.NewGuid(), 
                PatientId = "PAT123" 
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Appointment not found."));
        }
    }
}
