using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Domain.Context;
using NUnit.Framework;
using System;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class UpdatePatientVitalsHandlerTests
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
            var handler = new UpdatePatientVitalsHandler(_context);
            Assert.That(handler, Is.Not.Null);
        }

        //[Test]
        //public void Handle_ShouldUpdatePatientVitals_WhenValidInput()
        //{
        //    // Arrange
        //    var patientId = Guid.NewGuid();
        //    var handler = new UpdatePatientVitalsHandler(_context);

        //    // Act
        //    var result = handler.Handle(new UpdatePatientVitalsCommand { PatientId = patientId });

        //    // Assert
        //    Assert.That(result, Is.True, "Patient vitals should be updated successfully.");
        //}

        //[Test]
        //public void Handle_ShouldThrowException_WhenInvalidInput()
        //{
        //    // Arrange
        //    var handler = new UpdatePatientVitalsHandler(_context);

        //    // Act & Assert
        //    Assert.Throws<Exception>(() => handler.Handle(new UpdatePatientVitalsCommand()),
        //        "Expected exception when input is invalid.");
        //}
    }
}
