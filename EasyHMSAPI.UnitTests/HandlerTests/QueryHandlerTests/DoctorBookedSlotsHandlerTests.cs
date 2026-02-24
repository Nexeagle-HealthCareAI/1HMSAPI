using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Data.Enums;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class DoctorBookedSlotsHandlerTests
    {
        private AppDbContext _context = null!;
        private DoctorBookedSlotsHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new DoctorBookedSlotsHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ReturnsBookedSlots()
        {
            // Arrange
            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            var hospitalId = Guid.NewGuid();
            var date = DateTime.Today;
            TimeSpan slotTime = new TimeSpan(10, 0, 0);

            var appointment = new Appointment
            {
                ApptId = Guid.NewGuid(),
                DoctorId = doctor.DoctorID,
                HospitalId = hospitalId,
                ApptDate = date,
                StartAt = date.Add(slotTime),
                CurrentStatusCode = "Booked"
            };
            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            var request = new DoctorBookedSlotsRequestModel
            {
                DoctorId = doctor.DoctorID,
                HospitalId = hospitalId,
                Date = date
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.BookedSlots, Has.Count.EqualTo(1));
            Assert.That(response.BookedSlots[0], Is.EqualTo(slotTime));
        }
        
         [Test]
        public async Task Handle_CancelledAppointment_NotReturned()
        {
            // Arrange
            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            var hospitalId = Guid.NewGuid();
            var date = DateTime.Today;

            var appointment = new Appointment
            {
                ApptId = Guid.NewGuid(),
                DoctorId = doctor.DoctorID,
                HospitalId = hospitalId,
                ApptDate = date,
                StartAt = date.AddHours(10),
                CurrentStatusCode = AppConstants.AppointmentStatus_Cancelled
            };
            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            var request = new DoctorBookedSlotsRequestModel
            {
                DoctorId = doctor.DoctorID,
                HospitalId = hospitalId,
                Date = date
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.BookedSlots, Is.Empty);
        }
    }
}
