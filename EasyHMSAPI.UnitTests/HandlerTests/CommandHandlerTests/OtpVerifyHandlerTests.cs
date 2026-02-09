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
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class OtpVerifyHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<IJwtAuthService> _jwtAuthServiceMock = null!;
        private Mock<IConfiguration> _configurationMock = null!;
        private Mock<IMaskingService> _maskingServiceMock = null!;
        private OtpVerifyHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _jwtAuthServiceMock = new Mock<IJwtAuthService>();
            _configurationMock = new Mock<IConfiguration>();
            _maskingServiceMock = new Mock<IMaskingService>();

            _configurationMock.SetupGet(x => x["Security:OtpPepper"]).Returns("test-pepper");
            _maskingServiceMock.Setup(m => m.IsMaskingEnabled()).Returns(false);
            _maskingServiceMock.Setup(m => m.Mask(It.IsAny<string>())).Returns((string s) => s);

            _handler = new OtpVerifyHandler(_context, _jwtAuthServiceMock.Object, _configurationMock.Object, _maskingServiceMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ValidOtp_VerifiesSuccessfully()
        {
            // Arrange
            var user = TestDataFactory.SeedUser(_context, phone: "1234567890");
            var otp = "123456";
            TestDataFactory.GetOrCreateUserAuth(_context, user, otp: otp, otpExpireAt: DateTime.Now.AddMinutes(10));
            
            var userProfile = new UserProfile { UserID = user.UserID, FullName = "Test User" };
            _context.UserProfiles.Add(userProfile);
            await _context.SaveChangesAsync();

            _jwtAuthServiceMock.Setup(x => x.GenerateJwtToken(It.IsAny<List<Claim>>())).Returns("fake-jwt-token");

            var request = new OtpVerifyRequestModel { MobileNumber = "1234567890", Otp = otp };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.AccessToken, Is.EqualTo("fake-jwt-token"));
            
            var updatedAuth = await _context.UserAuths.FirstOrDefaultAsync(ua => ua.UserID == user.UserID);
            Assert.That(updatedAuth!.IsOtpUsed, Is.True);
        }

        [Test]
        public async Task Handle_InvalidOtp_ReturnsFailure()
        {
            // Arrange
            var user = TestDataFactory.SeedUser(_context, phone: "1234567890");
            TestDataFactory.GetOrCreateUserAuth(_context, user, otp: "123456", otpExpireAt: DateTime.Now.AddMinutes(10));
            
            var request = new OtpVerifyRequestModel { MobileNumber = "1234567890", Otp = "999999" };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("Invalid or already used OTP"));
        }
    }
}
