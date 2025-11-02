using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Domain.Context;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class HospitalRegisterHandlerTests
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
            var handler = new HospitalRegisterHandler(_context);
            Assert.That(handler, Is.Not.Null);
        }

        //[Test]
        //public void Handle_ShouldRegisterHospital_WhenValidInput()
        //{
        //    // Arrange
        //    var hospitalId = Guid.NewGuid();
        //    var handler = new HospitalRegisterHandler(_context);

        //    // Act
        //    var result = handler.Handle(new HospitalRegisterCommand { HospitalId = hospitalId });

        //    // Assert
        //    Assert.That(result, Is.True, "Hospital should be registered successfully.");
        //}

        //[Test]
        //public void Handle_ShouldThrowException_WhenInvalidInput()
        //{
        //    // Arrange
        //    var handler = new HospitalRegisterHandler(_context);

        //    // Act & Assert
        //    Assert.Throws<Exception>(() => handler.Handle(new HospitalRegisterCommand()),
        //        "Expected exception when input is invalid.");
        //}
    }
}
