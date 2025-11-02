using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Domain.Context;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class OtpSendHandlerTests
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
        //    _context?.Dispose();
        //    InMemoryDbContextFactory.Destroy(_context);
        //}

        //[Test, Ignore("TODO: Implement test logic")]
        //public void Constructor_Smoke()
        //{
        //    var handler = new OtpSendHandler(_context, _otpServiceMock.Object);
        //    Assert.That(handler, Is.Not.Null);
        //}

        //[Test]
        //public void Handle_ShouldSendOtp_WhenValidInput()
        //{
        //    // Arrange
        //    var otpId = Guid.NewGuid();
        //    var handler = new OtpSendHandler(_context, _otpServiceMock.Object);

        //    // Act
        //    var result = handler.Handle(new OtpSendCommand { OtpId = otpId });

        //    // Assert
        //    Assert.That(result, Is.True, "OTP should be sent successfully.");
        //}

        //[Test]
        //public void Handle_ShouldThrowException_WhenInvalidInput()
        //{
        //    // Arrange
        //    var handler = new OtpSendHandler(_context, _otpServiceMock.Object);

        //    // Act & Assert
        //    Assert.Throws<Exception>(() => handler.Handle(new OtpSendCommand()),
        //        "Expected exception when input is invalid.");
        //}
    }
}
