using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Domain.Context;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class UpdateDoctorPreferenceSettingHandlerTests
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
            var handler = new UpdateDoctorPreferenceSettingHandler(_context);
            Assert.That(handler, Is.Not.Null);
        }

        //[Test]
        //public void Handle_ShouldUpdateDoctorPreferenceSetting_WhenValidInput()
        //{
        //    // Arrange
        //    var settingId = Guid.NewGuid();
        //    var handler = new UpdateDoctorPreferenceSettingHandler(_context);

        //    // Act
        //    var result = handler.Handle(new UpdateDoctorPreferenceSettingCommand { SettingId = settingId });

        //    // Assert
        //    Assert.That(result, Is.True, "Doctor preference setting should be updated successfully.");
        //}

        //[Test]
        //public void Handle_ShouldThrowException_WhenInvalidInput()
        //{
        //    // Arrange
        //    var handler = new UpdateDoctorPreferenceSettingHandler(_context);

        //    // Act & Assert
        //    Assert.Throws<Exception>(() => handler.Handle(new UpdateDoctorPreferenceSettingCommand()),
        //        "Expected exception when input is invalid.");
        //}
    }
}
