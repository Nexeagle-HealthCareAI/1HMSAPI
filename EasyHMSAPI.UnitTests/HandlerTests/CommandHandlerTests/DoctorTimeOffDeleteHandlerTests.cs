using System;
using Moq;
using NUnit.Framework;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.Services.Interfaces;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class DoctorTimeOffDeleteHandlerTests
    {
        //private AppDbContext _context = null!;
        //private Mock<IExceptionService> _exceptionServiceMock = null!;

        //[SetUp]
        //public void SetUp()
        //{
        //    _context = InMemoryDbContextFactory.CreateContext();
        //    _exceptionServiceMock = new Mock<IExceptionService>();
        //}

        //[TearDown]
        //public void TearDown()
        //{
        //    _context?.Dispose();
        //    InMemoryDbContextFactory.Destroy(_context);
        //}

        //[Test, Ignore("TODO: Implement test logic")]
        //public void Constructor_Smoke()
        //{
        //    var handler = new DoctorTimeOffDeleteHandler(_context, _exceptionServiceMock.Object);
        //    Assert.That(handler, Is.Not.Null);
        //}

        //[Test]
        //public void Handle_ShouldDeleteDoctorTimeOff_WhenTimeOffExists()
        //{
        //    // Arrange
        //    var timeOffId = Guid.NewGuid();
        //    var handler = new DoctorTimeOffDeleteHandler(_context, _exceptionServiceMock.Object);

        //    // Act
        //    var result = handler.Handle(new DoctorTimeOffDeleteCommand { TimeOffId = timeOffId });

        //    // Assert
        //    Assert.That(result, Is.True, "Doctor time off should be deleted successfully.");
        //}

        //[Test]
        //public void Handle_ShouldThrowException_WhenTimeOffDoesNotExist()
        //{
        //    // Arrange
        //    var timeOffId = Guid.NewGuid();
        //    var handler = new DoctorTimeOffDeleteHandler(_context, _exceptionServiceMock.Object);

        //    // Act & Assert
        //    Assert.Throws<Exception>(() => handler.Handle(new DoctorTimeOffDeleteCommand { TimeOffId = timeOffId }),
        //        "Expected exception when doctor time off does not exist.");
        //}
    }
}
