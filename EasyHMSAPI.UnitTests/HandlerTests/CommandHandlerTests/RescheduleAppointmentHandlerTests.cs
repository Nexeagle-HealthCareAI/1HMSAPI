using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Domain.Context;
using NUnit.Framework;
using System;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class RescheduleAppointmentHandlerTests
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

        //[Test, Ignore("TODO: Implement test logic")]
        //public void Constructor_Smoke()
        //{
        //    var handler = new RescheduleAppointmentHandler(_context);
        //    Assert.That(handler, Is.Not.Null);
        //}

        //[Test]
        //public void Handle_ShouldRescheduleAppointment_WhenValidInput()
        //{
        //    // Arrange
        //    var appointmentId = Guid.NewGuid();
        //    var handler = new RescheduleAppointmentHandler(_context);

        //    // Act
        //    var result = handler.Handle(new RescheduleAppointmentCommand { AppointmentId = appointmentId });

        //    // Assert
        //    Assert.That(result, Is.True, "Appointment should be rescheduled successfully.");
        //}

        //[Test]
        //public void Handle_ShouldThrowException_WhenInvalidInput()
        //{
        //    // Arrange
        //    var handler = new RescheduleAppointmentHandler(_context);

        //    // Act & Assert
        //    Assert.Throws<Exception>(() => handler.Handle(new RescheduleAppointmentCommand()),
        //        "Expected exception when input is invalid.");
        //}
    }
}
