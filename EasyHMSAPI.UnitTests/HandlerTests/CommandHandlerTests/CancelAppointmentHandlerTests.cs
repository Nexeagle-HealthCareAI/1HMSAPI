using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Data.Enums;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class CancelAppointmentHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<ISmsService> _smsServiceMock = null!;
        private CancelAppointmentHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _smsServiceMock = new Mock<ISmsService>();
            _handler = new CancelAppointmentHandler(_context, _smsServiceMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ValidAppointment_CancelsAndSendsSms()
        {
            // Arrange
            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            var patient = new PatientRegistration 
            { 
                PatientId = Guid.NewGuid().ToString(),
                Mobile = "5551234567",
                FullName = "John Doe",
                // RegistrationNo = 1, // Removed
                HospitalId = Guid.NewGuid() // Add required field
            };
            _context.PatientRegistrations.Add(patient);
            
            var appointment = new Appointment
            {
                ApptId = Guid.NewGuid(),
                PatientId = patient.PatientId,
                DoctorId = doctor.DoctorID,
                CurrentStatusCode = "Booked",
                ApptDate = DateTime.Today,
                StartAt = DateTime.Now
            };
            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            _smsServiceMock.Setup(x => x.SendInvitationSmsAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            var request = new CancelAppointmentRequestModel
            {
                AppointmentId = appointment.ApptId, // Changed to AppointmentId
                // Reason = "Busy", // Removed as not in request model? Wait, need to check if Reason is in model.
                // Request Model defines AppointmentId and PatientId. Does it define Reason?
                // Checking Step 483: Only AppointmentId and PatientId.
                PatientId = patient.PatientId
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.FinalStatus, Is.EqualTo(AppConstants.AppointmentStatus_Cancelled));
            Assert.That(response.IsReminderSent, Is.True);
            
            var updatedAppt = await _context.Appointments.FindAsync(appointment.ApptId);
            Assert.That(updatedAppt.CurrentStatusCode, Is.EqualTo(AppConstants.AppointmentStatus_Cancelled));
        }

        [Test]
        public async Task Handle_AppointmentNotFound_ReturnsFailure()
        {
            // Arrange
            var request = new CancelAppointmentRequestModel 
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
