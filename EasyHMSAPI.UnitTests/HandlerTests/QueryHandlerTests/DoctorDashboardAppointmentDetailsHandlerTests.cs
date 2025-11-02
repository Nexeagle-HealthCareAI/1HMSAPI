using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Domain.Context;
using NUnit.Framework;
using System;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class DoctorDashboardAppointmentDetailsHandlerTests
    {
        private AppDbContext _context = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
        }

        [TearDown]
        public void TearDown()
        {
            _context?.Dispose();
            InMemoryDbContextFactory.Destroy(_context);
        }

        [Test, Ignore("TODO: Implement test logic")]
        public void Constructor_Smoke()
        {
            var handler = new DoctorDashboardAppointmentDetailsHandler(_context);
            Assert.That(handler, Is.Not.Null);
        }

        //[Test]
        //public void Handle_ShouldReturnAppointmentDetails_WhenValidDoctorId()
        //{
        //    // Arrange
        //    var doctorId = Guid.NewGuid();
        //    var handler = new DoctorDashboardAppointmentDetailsHandler(_context);

        //    // Act
        //    var appointmentDetails = handler.Handle(new DoctorDashboardAppointmentDetailsQuery { DoctorId = doctorId });

        //    // Assert
        //    Assert.That(appointmentDetails, Is.Not.Null, "Appointment details should be returned.");
        //}

        //[Test]
        //public void Handle_ShouldThrowException_WhenInvalidDoctorId()
        //{
        //    // Arrange
        //    var handler = new DoctorDashboardAppointmentDetailsHandler(_context);

        //    // Act & Assert
        //    Assert.Throws<Exception>(() => handler.Handle(new DoctorDashboardAppointmentDetailsQuery()),
        //        "Expected exception when doctor ID is invalid.");
        //}
    }
}
