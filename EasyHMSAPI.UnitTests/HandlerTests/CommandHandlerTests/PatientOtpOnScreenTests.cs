using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using Microsoft.Extensions.Configuration;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    /// <summary>On a Development server a tester can have the patient OTP shown on the page; nowhere else may it ever leave the server except on WhatsApp.</summary>
    [TestFixture]
    public class PatientOtpOnScreenTests
    {
        private AppDbContext _context = null!;
        private Mock<IWhatsAppMessagingService> _whatsApp = null!;
        private static CancellationToken Ct => CancellationToken.None;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _whatsApp = new Mock<IWhatsAppMessagingService>();
            // WhatsApp accepts the message; a refused send is covered in PatientOtpSendTests.
            _whatsApp.Setup(w => w.SendOtpAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private PatientOtpSendHandler Handler(string? show, string? env)
        {
            var mask = new Mock<IMaskingService>();
            mask.Setup(m => m.Mask(It.IsAny<string>())).Returns<string>(s => s);
            var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PatientAuth:ShowOtpOnScreen"] = show,
                ["ASPNETCORE_ENVIRONMENT"] = env,
            }).Build();
            return new PatientOtpSendHandler(_context, _whatsApp.Object, mask.Object, cfg);
        }

        [Test]
        public async Task Development_WithTheSettingOn_ReturnsTheCodeAndDoesNotSendWhatsApp()
        {
            var r = await Handler("true", "Development").Handle(new PatientOtpSendRequestModel { MobileNumber = "9876543210" }, Ct);

            Assert.That(r.Success, Is.True);
            Assert.That(r.DevCode, Does.Match("^[0-9]{6}$"));
            _whatsApp.Verify(w => w.SendOtpAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [TestCase("true", "Production")]
        [TestCase("true", null)]
        [TestCase("false", "Development")]
        [TestCase(null, "Development")]
        public async Task EverywhereElse_NoCodeIsReturned_AndWhatsAppIsUsed(string? show, string? env)
        {
            var r = await Handler(show, env).Handle(new PatientOtpSendRequestModel { MobileNumber = "9876543210" }, Ct);

            Assert.That(r.Success, Is.True);
            Assert.That(r.DevCode, Is.Null);
            _whatsApp.Verify(w => w.SendOtpAsync("9876543210", It.IsAny<string>()), Times.Once);
        }

        [Test]
        public async Task TheReturnedCodeIsTheOneThatLogsIn()
        {
            var handler = Handler("true", "Development");
            var sent = await handler.Handle(new PatientOtpSendRequestModel { MobileNumber = "9876543210" }, Ct);
            var row = _context.PublicPatientAuths.Find("9876543210")!;

            Assert.That(row.Otp, Is.EqualTo(sent.DevCode));
        }

        [Test]
        public async Task DailyCap_IsNotAppliedOnScreenInDevelopment_ButStillAppliesWhenWhatsAppIsUsed()
        {
            var screen = Handler("true", "Development");
            for (var i = 0; i < 7; i++)
            {
                // step past the 30 s cooldown between sends
                var row = _context.PublicPatientAuths.Find("9876543210");
                if (row != null) { row.OtpSentAt = System.DateTime.UtcNow.AddMinutes(-5); await _context.SaveChangesAsync(); }
                var r = await screen.Handle(new PatientOtpSendRequestModel { MobileNumber = "9876543210" }, Ct);
                Assert.That(r.Success, Is.True, $"send {i + 1}: {r.Message}");
            }

            _context.PublicPatientAuths.Find("9876543210")!.OtpSentAt = System.DateTime.UtcNow.AddMinutes(-5);
            await _context.SaveChangesAsync();
            var prod = Handler("true", "Production");
            var blocked = await prod.Handle(new PatientOtpSendRequestModel { MobileNumber = "9876543210" }, Ct);
            Assert.That(blocked.Success, Is.False);
            Assert.That(blocked.Message, Does.Contain("Too many OTP requests"));
        }

        [Test]
        public void DevCodeIsNotWrittenToJsonWhenAbsent() =>
            Assert.That(System.Text.Json.JsonSerializer.Serialize(new EasyHMSAPI.Application.ResponseModels.CommandResponseModels.PatientOtpSendResponseModel { Success = true }), Does.Not.Contain("DevCode").IgnoreCase);
    }
}
