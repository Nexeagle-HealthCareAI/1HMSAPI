using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Domain.Context;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class HospitalUpdateHandlerTests
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
            var handler = new HospitalUpdateHandler(_context);
            Assert.That(handler, Is.Not.Null);
        }

        //[Test]
        //public void Handle_ShouldUpdateHospital_WhenHospitalExists()
        //{
        //    // Arrange
        //    var hospitalId = Guid.NewGuid();
        //    var handler = new HospitalUpdateHandler(_context);

        //    // Act
        //    var result = handler.Handle(new HospitalUpdateCommand { HospitalId = hospitalId });

        //    // Assert
        //    Assert.That(result, Is.True, "Hospital should be updated successfully.");
        //}

        //[Test]
        //public void Handle_ShouldThrowException_WhenHospitalDoesNotExist()
        //{
        //    // Arrange
        //    var hospitalId = Guid.NewGuid();
        //    var handler = new HospitalUpdateHandler(_context);

        //    // Act & Assert
        //    Assert.Throws<Exception>(() => handler.Handle(new HospitalUpdateCommand { HospitalId = hospitalId }),
        //        "Expected exception when hospital does not exist.");
        //}
    }
}
