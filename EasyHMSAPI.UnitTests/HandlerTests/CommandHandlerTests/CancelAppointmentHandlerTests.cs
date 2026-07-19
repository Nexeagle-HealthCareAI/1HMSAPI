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
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class CancelAppointmentHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<ISmsService> _mockSmsService = null!;
        private CancelAppointmentHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _mockSmsService = new Mock<ISmsService>();
            _handler = new CancelAppointmentHandler(_context, _mockSmsService.Object, NullLogger<CancelAppointmentHandler>.Instance, new MemoryCache(new MemoryCacheOptions()));
        }

        [TearDown]
        public void TearDown()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        [Test]
        public async Task Handle_AppointmentNotFound_ReturnsError()
        {
            // Arrange
            var request = new CancelAppointmentRequestModel
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

        [Test]
        public async Task Handle_DoctorRevoked_ReturnsError()
        {
            // Arrange
            var doctorId = Guid.NewGuid();
            var hospitalId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var patientId = "PAT123";
            var appointmentId = Guid.NewGuid();

            var user = TestEntityFactory.CreateUser(userId);
            user.UserStatusId = (int)UserStatusEnum.Revoked;
            _context.Users.Add(user);
            
            _context.Doctors.Add(TestEntityFactory.CreateDoctor(doctorId, userId));
            _context.Hospitals.Add(TestEntityFactory.CreateHospital(hospitalId, userId));
            _context.Appointments.Add(TestEntityFactory.CreateAppointment(appointmentId, hospitalId, doctorId, patientId));
            await _context.SaveChangesAsync();

            var request = new CancelAppointmentRequestModel
            {
                AppointmentId = appointmentId,
                PatientId = patientId,
                HospitalId = hospitalId
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Doctor is not active or has been revoked."));
        }

        [Test]
        public async Task Handle_Success_CancelsAppointment()
        {
            // Arrange
            var doctorId = Guid.NewGuid();
            var hospitalId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var patientId = "PAT123";
            var appointmentId = Guid.NewGuid();

             var user = TestEntityFactory.CreateUser(userId);
            user.UserStatusId = (int)UserStatusEnum.Active; // Active
            _context.Users.Add(user);

            _context.Doctors.Add(TestEntityFactory.CreateDoctor(doctorId, userId));
            _context.Hospitals.Add(TestEntityFactory.CreateHospital(hospitalId, userId));
            
            var appointment = TestEntityFactory.CreateAppointment(appointmentId, hospitalId, doctorId, patientId);
            appointment.StatusHistoryJson = "[]"; 
            _context.Appointments.Add(appointment);
            
            _context.PatientRegistrations.Add(TestEntityFactory.CreatePatientRegistration(hospitalId, patientId, "John Doe"));
            await _context.SaveChangesAsync();

            _mockSmsService.Setup(x => x.SendInvitationSmsAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            var request = new CancelAppointmentRequestModel
            {
                AppointmentId = appointmentId,
                PatientId = patientId,
                HospitalId = hospitalId,
                Reason = "Patient requested",
                LoggedInUserName = "Front Desk User",
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.FinalStatus, Is.EqualTo(AppConstants.AppointmentStatus_Cancelled));

            var cancelledAppt = await _context.Appointments.FindAsync(appointmentId);
            Assert.That(cancelledAppt!.CurrentStatusCode, Is.EqualTo(AppConstants.AppointmentStatus_Cancelled));
            Assert.That(cancelledAppt.CancellationReason, Is.EqualTo("Patient requested"));
            Assert.That(cancelledAppt.CancelledBy, Is.EqualTo("Front Desk User"));
            Assert.That(cancelledAppt.CancelledAt, Is.Not.Null);
        }

        [Test]
        public async Task Handle_AlreadyCancelled_ReturnsError()
        {
            var doctorId = Guid.NewGuid();
            var hospitalId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var patientId = "PAT123";
            var appointmentId = Guid.NewGuid();

            _context.Users.Add(TestEntityFactory.CreateUser(userId));
            _context.Doctors.Add(TestEntityFactory.CreateDoctor(doctorId, userId));
            _context.Hospitals.Add(TestEntityFactory.CreateHospital(hospitalId, userId));

            var appointment = TestEntityFactory.CreateAppointment(appointmentId, hospitalId, doctorId, patientId);
            appointment.CurrentStatusCode = AppConstants.AppointmentStatus_Cancelled;
            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            var request = new CancelAppointmentRequestModel
            {
                AppointmentId = appointmentId,
                PatientId = patientId,
                HospitalId = hospitalId,
            };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("This appointment is already cancelled."));
        }

        [Test]
        public async Task Handle_AlreadyCompleted_ReturnsError()
        {
            var doctorId = Guid.NewGuid();
            var hospitalId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var patientId = "PAT123";
            var appointmentId = Guid.NewGuid();

            _context.Users.Add(TestEntityFactory.CreateUser(userId));
            _context.Doctors.Add(TestEntityFactory.CreateDoctor(doctorId, userId));
            _context.Hospitals.Add(TestEntityFactory.CreateHospital(hospitalId, userId));

            var appointment = TestEntityFactory.CreateAppointment(appointmentId, hospitalId, doctorId, patientId);
            appointment.CurrentStatusCode = AppConstants.AppointmentStatus_Completed;
            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            var request = new CancelAppointmentRequestModel
            {
                AppointmentId = appointmentId,
                PatientId = patientId,
                HospitalId = hospitalId,
            };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Cannot cancel a completed appointment."));
        }

        [Test]
        public async Task Handle_CrossHospitalAppointmentId_ReturnsNotFound()
        {
            var doctorId = Guid.NewGuid();
            var hospitalId = Guid.NewGuid();
            var otherHospitalId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var patientId = "PAT123";
            var appointmentId = Guid.NewGuid();

            _context.Users.Add(TestEntityFactory.CreateUser(userId));
            _context.Doctors.Add(TestEntityFactory.CreateDoctor(doctorId, userId));
            _context.Hospitals.Add(TestEntityFactory.CreateHospital(hospitalId, userId));
            _context.Appointments.Add(TestEntityFactory.CreateAppointment(appointmentId, hospitalId, doctorId, patientId));
            await _context.SaveChangesAsync();

            var request = new CancelAppointmentRequestModel
            {
                AppointmentId = appointmentId,
                PatientId = patientId,
                HospitalId = otherHospitalId,
            };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Appointment not found."));
        }

         [Test]
        public async Task Handle_Success_WithTokenReset()
        {
            // Arrange
            var doctorId = Guid.NewGuid();
            var hospitalId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var patientId = "PAT123";
            var appointmentId = Guid.NewGuid();
            var tokenId = Guid.NewGuid();

             var user = TestEntityFactory.CreateUser(userId);
            user.UserStatusId = (int)UserStatusEnum.Active;
            _context.Users.Add(user);

            _context.Doctors.Add(TestEntityFactory.CreateDoctor(doctorId, userId));
            _context.Hospitals.Add(TestEntityFactory.CreateHospital(hospitalId, userId));
            
            var appointment = TestEntityFactory.CreateAppointment(appointmentId, hospitalId, doctorId, patientId);
            appointment.StatusHistoryJson = "[]";
            _context.Appointments.Add(appointment);
            
            _context.AppointmentTokens.Add(new AppointmentToken
            {
                TokenId = tokenId,
                ApptId = appointmentId,
                TokenNo = 10,
                TokenDate = DateTime.UtcNow,
                 HospitalId = hospitalId,
                 DoctorId = doctorId
            });
            
            _context.PatientRegistrations.Add(TestEntityFactory.CreatePatientRegistration(hospitalId, patientId, "John Doe"));
            await _context.SaveChangesAsync();

            var request = new CancelAppointmentRequestModel
            {
                AppointmentId = appointmentId,
                PatientId = patientId,
                HospitalId = hospitalId,
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            var token = await _context.AppointmentTokens.FindAsync(tokenId);
            Assert.That(token!.TokenNo, Is.EqualTo(0));
        }

        [Test]
        public async Task Handle_Success_WithTokenReset_WhenStatusHistoryJsonWasNull()
        {
            // Regression test: the handler used to gate the token-reset + history write behind
            // `if (appt.StatusHistoryJson != null)`, checked BEFORE that field was ever assigned —
            // so a brand-new/legacy appointment whose StatusHistoryJson starts null never got its
            // token reset, even though CurrentStatusCode still flipped to CANCELLED.
            var doctorId = Guid.NewGuid();
            var hospitalId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var patientId = "PAT123";
            var appointmentId = Guid.NewGuid();
            var tokenId = Guid.NewGuid();

            var user = TestEntityFactory.CreateUser(userId);
            user.UserStatusId = (int)UserStatusEnum.Active;
            _context.Users.Add(user);

            _context.Doctors.Add(TestEntityFactory.CreateDoctor(doctorId, userId));
            _context.Hospitals.Add(TestEntityFactory.CreateHospital(hospitalId, userId));

            var appointment = TestEntityFactory.CreateAppointment(appointmentId, hospitalId, doctorId, patientId);
            // StatusHistoryJson intentionally left null (factory default) — the regression scenario.
            Assert.That(appointment.StatusHistoryJson, Is.Null);
            _context.Appointments.Add(appointment);

            _context.AppointmentTokens.Add(new AppointmentToken
            {
                TokenId = tokenId,
                ApptId = appointmentId,
                TokenNo = 7,
                TokenDate = DateTime.UtcNow,
                HospitalId = hospitalId,
                DoctorId = doctorId
            });

            _context.PatientRegistrations.Add(TestEntityFactory.CreatePatientRegistration(hospitalId, patientId, "John Doe"));
            await _context.SaveChangesAsync();

            var request = new CancelAppointmentRequestModel
            {
                AppointmentId = appointmentId,
                PatientId = patientId,
                HospitalId = hospitalId,
            };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            var cancelledAppt = await _context.Appointments.FindAsync(appointmentId);
            Assert.That(cancelledAppt!.CurrentStatusCode, Is.EqualTo(AppConstants.AppointmentStatus_Cancelled));
            Assert.That(cancelledAppt.StatusHistoryJson, Is.Not.Null.And.Not.Empty);
            var token = await _context.AppointmentTokens.FindAsync(tokenId);
            Assert.That(token!.TokenNo, Is.EqualTo(0));
        }
    }
}
