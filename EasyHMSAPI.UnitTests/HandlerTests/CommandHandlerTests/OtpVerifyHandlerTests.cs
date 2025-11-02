using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Domain.Context;
using Moq;
using NUnit.Framework;
using System;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class OtpVerifyHandlerTests
    {
        //private AppDbContext _context = null!;
        //private Mock<IOtpService> _otpServiceMock = null!;

        //[SetUp]
        //public void SetUp()
        //{
        //    _context = InMemoryDbContextFactory.CreateContext();
        //    _otpServiceMock = new Mock<IOtpService>();
        //}

        //[TearDown]
        //public void TearDown()
        //{
        //    InMemoryDbContextFactory.Destroy(_context);
        //}

        //[Test, Ignore("TODO: Implement test logic")]
        //public void Constructor_Smoke()
        //{
        //    var handler = new OtpVerifyHandler(_context, _otpServiceMock.Object);
        //    Assert.That(handler, Is.Not.Null);
        //}

        //[Test]
        //public void Handle_ShouldVerifyOtp_WhenValidInput()
        //{
        //    // Arrange
        //    var otpId = Guid.NewGuid();
        //    var handler = new OtpVerifyHandler(_context, _otpServiceMock.Object);

        //    // Act
        //    var result = handler.Handle(new OtpVerifyCommand { OtpId = otpId });

        //    // Assert
        //    Assert.That(result, Is.True, "OTP should be verified successfully.");
        //}

        //[Test]
        //public void Handle_ShouldThrowException_WhenInvalidInput()
        //{
        //    // Arrange
        //    var handler = new OtpVerifyHandler(_context, _otpServiceMock.Object);

        //    // Act & Assert
        //    Assert.Throws<Exception>(() => handler.Handle(new OtpVerifyCommand()),
        //        "Expected exception when input is invalid.");
        //}
    }
}
