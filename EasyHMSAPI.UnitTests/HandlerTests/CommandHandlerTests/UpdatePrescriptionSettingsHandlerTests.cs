using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Domain.Context;
using NUnit.Framework;
using System;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class UpdatePrescriptionSettingsHandlerTests
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
            var handler = new UpdatePrescriptionSettingsHandler(_context);
            Assert.That(handler, Is.Not.Null);
        }

        //[Test]
        //public void Handle_ShouldUpdatePrescriptionSettings_WhenValidInput()
        //{
        //    // Arrange
        //    var settingsId = Guid.NewGuid();
        //    var handler = new UpdatePrescriptionSettingsHandler(_context);

        //    // Act
        //    var result = handler.Handle(new UpdatePrescriptionSettingsCommand { SettingsId = settingsId });

        //    // Assert
        //    Assert.That(result, Is.True, "Prescription settings should be updated successfully.");
        //}

        //[Test]
        //public void Handle_ShouldThrowException_WhenInvalidInput()
        //{
        //    // Arrange
        //    var handler = new UpdatePrescriptionSettingsHandler(_context);

        //    // Act & Assert
        //    Assert.Throws<Exception>(() => handler.Handle(new UpdatePrescriptionSettingsCommand()),
        //        "Expected exception when input is invalid.");
        //}
    }
}
