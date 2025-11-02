using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Domain.Context;
using NUnit.Framework;
using System;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class RegisterAppointmentHandlerTests
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
        //    var handler = new RegisterAppointmentHandler(_context);
        //    Assert.That(handler, Is.Not.Null);
        //}

        //[Test]
        //public void Handle_ShouldRegisterAppointment_WhenValidInput()
        //{
        //    // Arrange
        //    var appointmentId = Guid.NewGuid();
        //    var handler = new RegisterAppointmentHandler(_context);

        //    // Act
        //    var result = handler.Handle(new RegisterAppointmentCommand { AppointmentId = appointmentId });

        //    // Assert
        //    Assert.That(result, Is.True, "Appointment should be registered successfully.");
        //}

        //[Test]
        //public void Handle_ShouldThrowException_WhenInvalidInput()
        //{
        //    // Arrange
        //    var handler = new RegisterAppointmentHandler(_context);

        //    // Act & Assert
        //    Assert.Throws<Exception>(() => handler.Handle(new RegisterAppointmentCommand()),
        //        "Expected exception when input is invalid.");
        //}
    }
}
