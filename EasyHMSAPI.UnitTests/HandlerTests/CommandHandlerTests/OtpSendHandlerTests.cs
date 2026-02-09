using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class OtpSendHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<ISmsService> _smsServiceMock = null!;
        private Mock<IEmailService> _emailServiceMock = null!;
        private Mock<IWhatsAppMessagingService> _whatsAppServiceMock = null!;
        private Mock<IConfiguration> _configurationMock = null!;
        private Mock<IMaskingService> _maskingServiceMock = null!;
        private OtpSendHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _smsServiceMock = new Mock<ISmsService>();
            _emailServiceMock = new Mock<IEmailService>();
            _whatsAppServiceMock = new Mock<IWhatsAppMessagingService>();
            _configurationMock = new Mock<IConfiguration>();
            _maskingServiceMock = new Mock<IMaskingService>();

            _configurationMock.SetupGet(x => x["Security:OtpPepper"]).Returns("test-pepper");
            _maskingServiceMock.Setup(m => m.IsMaskingEnabled()).Returns(false);
            _maskingServiceMock.Setup(m => m.Mask(It.IsAny<string>())).Returns((string s) => s);

            _handler = new OtpSendHandler(
                _context, 
                _smsServiceMock.Object, 
                _emailServiceMock.Object, 
                _whatsAppServiceMock.Object, 
                _configurationMock.Object,
                _maskingServiceMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ValidUser_SendsOtpViaWhatsApp()
        {
            // Arrange
            var user = TestDataFactory.SeedUser(_context, phone: "1234567890", email: "test@test.com");
            var userAuth = new UserAuth { UserID = user.UserID, UserStatusId = 1 };
            _context.UserAuths.Add(userAuth);
            await _context.SaveChangesAsync();

            _whatsAppServiceMock.Setup(x => x.SendOtpAsync(user.MobileNumber, It.IsAny<string>()))
                .ReturnsAsync(true);

            var request = new OtpSendRequestModel { MobileNumber = "1234567890" };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.IsWhatsappSent, Is.True);
            Assert.That(response.UserId, Is.EqualTo(user.UserID));
            
            var updatedAuth = await _context.UserAuths.FirstOrDefaultAsync(ua => ua.UserID == user.UserID);
            Assert.That(updatedAuth!.Otp, Is.Not.Null);
            Assert.That(updatedAuth.OtpExpireAt, Is.GreaterThan(DateTime.UtcNow));
        }

        [Test]
        public async Task Handle_UserNotFound_ReturnsFailure()
        {
            // Arrange
            var request = new OtpSendRequestModel { MobileNumber = "9999999999" };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("User not found."));
        }
    }
}
