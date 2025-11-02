using System;
using Moq;
using NUnit.Framework;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.Services.Interfaces;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class DoctorOverrideCreateHandlerTests
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
        //    var handler = new DoctorOverrideCreateHandler(_context, _exceptionServiceMock.Object);
        //    Assert.That(handler, Is.Not.Null);
        //}

        //[Test]
        //public void Handle_ShouldCreateDoctorOverride_WhenValidInput()
        //{
        //    // Arrange
        //    var overrideId = Guid.NewGuid();
        //    var handler = new DoctorOverrideCreateHandler(_context, _exceptionServiceMock.Object);

        //    // Act
        //    var result = handler.Handle(new DoctorOverrideCreateCommand { OverrideId = overrideId });

        //    // Assert
        //    Assert.That(result, Is.True, "Doctor override should be created successfully.");
        //}

        //[Test]
        //public void Handle_ShouldThrowException_WhenInvalidInput()
        //{
        //    // Arrange
        //    var handler = new DoctorOverrideCreateHandler(_context, _exceptionServiceMock.Object);

        //    // Act & Assert
        //    Assert.Throws<Exception>(() => handler.Handle(new DoctorOverrideCreateCommand()),
        //        "Expected exception when input is invalid.");
        //}
    }
}
