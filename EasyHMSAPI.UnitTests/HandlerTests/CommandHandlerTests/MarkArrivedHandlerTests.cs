using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class MarkArrivedHandlerTests
    {
        private AppDbContext _context = null!;
        private MarkArrivedHandler _handler = null!;
        private Guid _hospitalId;
        private Guid _doctorId;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new MarkArrivedHandler(_context);
            _hospitalId = Guid.NewGuid();
            _doctorId = Guid.NewGuid();
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private Appointment SeedAppointment(Guid? doctorId = null)
        {
            var appointment = new Appointment
            {
                ApptId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                DoctorId = doctorId ?? _doctorId,
                PatientId = "PT001",
                ApptDate = DateTime.UtcNow.Date,
                StartAt = DateTime.UtcNow,
                EndAt = DateTime.UtcNow.AddMinutes(15),
                CurrentStatusCode = "PRE_APPOINTMENT",
                LastStatusCodeAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
            };
            _context.Appointments.Add(appointment);
            _context.SaveChanges();
            return appointment;
        }

        [Test]
        public async Task Handle_NoGeofenceRequired_IssuesTokenEvenWithoutHospitalLocation()
        {
            var appt = SeedAppointment();

            var response = await _handler.Handle(new MarkArrivedRequestModel { AppointmentId = appt.ApptId, DoctorId = _doctorId }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.TokenNo, Is.EqualTo(1));

            var token = await _context.AppointmentTokens.FirstAsync(t => t.ApptId == appt.ApptId);
            Assert.That(token.ArrivalMethod, Is.EqualTo(AppConstants.QueueArrivalMethod_StaffOverride));
        }

        [Test]
        public async Task Handle_DoctorMismatch_Rejects()
        {
            var appt = SeedAppointment(doctorId: Guid.NewGuid());

            var response = await _handler.Handle(new MarkArrivedRequestModel { AppointmentId = appt.ApptId, DoctorId = _doctorId }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
        }

        [Test]
        public async Task Handle_AppointmentNotFound_Rejects()
        {
            var response = await _handler.Handle(new MarkArrivedRequestModel { AppointmentId = Guid.NewGuid(), DoctorId = _doctorId }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
        }
    }
}
