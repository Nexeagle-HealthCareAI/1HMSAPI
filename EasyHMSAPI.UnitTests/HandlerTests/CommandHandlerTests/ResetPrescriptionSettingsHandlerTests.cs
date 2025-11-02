using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Domain.Context;
using NUnit.Framework;
using System;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class ResetPrescriptionSettingsHandlerTests
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
            var handler = new ResetPrescriptionSettingsHandler(_context);
            Assert.That(handler, Is.Not.Null);
        }

        //    [Test]
        //    public void Handle_ShouldResetPrescriptionSettings_WhenValidInput()
        //    {
        //        // Arrange
        //        var settingsId = Guid.NewGuid();
        //        var handler = new ResetPrescriptionSettingsHandler(_context);

        //        // Act
        //        var result = handler.Handle(new ResetPrescriptionSettingsCommand { SettingsId = settingsId });

        //        // Assert
        //        Assert.That(result, Is.True, "Prescription settings should be reset successfully.");
        //    }

        //    [Test]
        //    public void Handle_ShouldThrowException_WhenInvalidInput()
        //    {
        //        // Arrange
        //        var handler = new ResetPrescriptionSettingsHandler(_context);

        //        // Act & Assert
        //        Assert.Throws<Exception>(() => handler.Handle(new ResetPrescriptionSettingsCommand()),
        //            "Expected exception when input is invalid.");
        //    }
    }
}
