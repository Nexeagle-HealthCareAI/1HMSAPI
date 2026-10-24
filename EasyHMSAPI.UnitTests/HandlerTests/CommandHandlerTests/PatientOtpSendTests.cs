using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    /// <summary>Doctor Dekho WhatsApp OTP send: "sent" must mean WhatsApp accepted the message.</summary>
    [TestFixture]
    public class PatientOtpSendTests
    {
        private AppDbContext _context = null!;
        private Mock<IWhatsAppMessagingService> _whatsApp = null!;
        private Mock<IMaskingService> _masking = null!;
        private static CancellationToken Ct => CancellationToken.None;
        private const string Mobile = "9876543210";

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _whatsApp = new Mock<IWhatsAppMessagingService>();
            _masking = new Mock<IMaskingService>();
            _masking.Setup(m => m.Mask(It.IsAny<string>())).Returns<string>(v => "masked:" + v);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        // A server that sends on WhatsApp (code not shown on the page): every server except Development with the setting on.
        private Task<PatientOtpSendResponseModel> Send(string mobile = Mobile)
        {
            var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = "Production",
            }).Build();
            return new PatientOtpSendHandler(_context, _whatsApp.Object, _masking.Object, cfg)
                .Handle(new PatientOtpSendRequestModel { MobileNumber = mobile }, Ct);
        }

        [Test]
        public async Task AcceptedByWhatsApp_ReportsSent_AndStoresTheCode()
        {
            _whatsApp.Setup(w => w.SendOtpAsync(Mobile, It.IsAny<string>())).ReturnsAsync(true);

            var r = await Send();

            Assert.That(r.Success, Is.True);
            var auth = await _context.PublicPatientAuths.SingleAsync(a => a.Mobile == Mobile);
            Assert.That(auth.Otp, Does.StartWith("masked:"));
            Assert.That(auth.OtpSendCount, Is.EqualTo(1));
        }

        [Test]
        public async Task RefusedByWhatsApp_ReportsFailure_StoresNoCode_AndAllowsAnImmediateRetry()
        {
            _whatsApp.Setup(w => w.SendOtpAsync(Mobile, It.IsAny<string>())).ReturnsAsync(false);

            var r = await Send();

            Assert.That(r.Success, Is.False);
            Assert.That(r.Message, Does.Contain("WhatsApp"));
            Assert.That(await _context.PublicPatientAuths.AnyAsync(a => a.Mobile == Mobile && a.Otp != null), Is.False);

            // No 30-second cooldown and no use of the daily allowance after a failed send.
            _whatsApp.Setup(w => w.SendOtpAsync(Mobile, It.IsAny<string>())).ReturnsAsync(true);
            var retry = await Send();
            Assert.That(retry.Success, Is.True);
            Assert.That((await _context.PublicPatientAuths.SingleAsync(a => a.Mobile == Mobile)).OtpSendCount, Is.EqualTo(1));
        }

        [Test]
        public async Task InvalidNumber_IsRejected_WithoutCallingWhatsApp()
        {
            var r = await Send("12345");
            Assert.That(r.Success, Is.False);
            _whatsApp.Verify(w => w.SendOtpAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }
    }
}
