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

        // ── daily cap ────────────────────────────────────────────────────────────────────────────────

        private Task<PatientOtpSendResponseModel> SendWith(Dictionary<string, string?> settings)
        {
            settings["ASPNETCORE_ENVIRONMENT"] = "Production";
            var cfg = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
            return new PatientOtpSendHandler(_context, _whatsApp.Object, _masking.Object, cfg)
                .Handle(new PatientOtpSendRequestModel { MobileNumber = Mobile }, Ct);
        }

        private async Task UseUpTheDay(int sends)
        {
            _context.PublicPatientAuths.Add(new EasyHMSAPI.Domain.Entities.PublicPatientAuth
            {
                Mobile = Mobile,
                CreatedAt = System.DateTime.UtcNow.AddHours(-3),
                OtpWindowStartAt = System.DateTime.UtcNow.AddHours(-3),
                OtpSendCount = sends,
                OtpSentAt = System.DateTime.UtcNow.AddMinutes(-5),
            });
            await _context.SaveChangesAsync();
        }

        [Test]
        public async Task CapReached_SaysWhenToTryAgain()
        {
            _whatsApp.Setup(w => w.SendOtpAsync(Mobile, It.IsAny<string>())).ReturnsAsync(true);
            await UseUpTheDay(5);

            var r = await SendWith(new Dictionary<string, string?>());

            Assert.That(r.Success, Is.False);
            Assert.That(r.Message, Does.StartWith("Too many OTP requests"));
            Assert.That(r.Message, Does.Contain("You can ask for a new code after"));
            Assert.That(r.RetryAfterSeconds, Is.InRange(20 * 3600, 21 * 3600 + 60));
            _whatsApp.Verify(w => w.SendOtpAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public async Task TesterNumber_SkipsTheCap()
        {
            _whatsApp.Setup(w => w.SendOtpAsync(Mobile, It.IsAny<string>())).ReturnsAsync(true);
            await UseUpTheDay(5);

            var r = await SendWith(new Dictionary<string, string?> { ["PatientAuth:UnlimitedOtpMobiles"] = "9000000000, " + Mobile });

            Assert.That(r.Success, Is.True, r.Message);
        }

        [Test]
        public async Task CapCanBeRaisedInSettings()
        {
            _whatsApp.Setup(w => w.SendOtpAsync(Mobile, It.IsAny<string>())).ReturnsAsync(true);
            await UseUpTheDay(5);

            var r = await SendWith(new Dictionary<string, string?> { ["PatientAuth:MaxOtpSendsPerDay"] = "10" });

            Assert.That(r.Success, Is.True, r.Message);
        }

        [Test]
        public void IndiaTime_ReadsAsTodayOrTomorrow()
        {
            var now = new System.DateTime(2026, 10, 24, 15, 0, 0, System.DateTimeKind.Utc);           // 8:30 PM IST
            Assert.That(PatientOtpSendHandler.IndiaTime(now.AddHours(1), now), Is.EqualTo("9:30 PM"));
            Assert.That(PatientOtpSendHandler.IndiaTime(now.AddHours(10), now), Is.EqualTo("tomorrow 6:30 AM"));
        }
    }
}
