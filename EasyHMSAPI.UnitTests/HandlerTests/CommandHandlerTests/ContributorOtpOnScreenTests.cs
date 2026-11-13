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
    /// <summary>On a development server the sign-in code can be shown on the Doctor Dekho page, so testers need no working WhatsApp number.</summary>
    [TestFixture]
    public class ContributorOtpOnScreenTests
    {
        private AppDbContext _context = null!;
        private Mock<IWhatsAppMessagingService> _whatsApp = null!;
        private static CancellationToken Ct => CancellationToken.None;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _whatsApp = new Mock<IWhatsAppMessagingService>();
            _whatsApp.Setup(w => w.SendOtpAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false); // the phone cannot get WhatsApp
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private static IConfiguration Config(string? showOnScreen, string? environment) =>
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = "test-secret-key-1234567890-abcdefghij", ["HealthWiki:ShowOtpOnScreen"] = showOnScreen, ["ASPNETCORE_ENVIRONMENT"] = environment,
            }).Build();

        private Task<Application.ResponseModels.CommandResponseModels.ContributorOtpSendResponseModel> Send(IConfiguration c) =>
            new ContributorOtpSendHandler(_context, _whatsApp.Object, c).Handle(new ContributorOtpSendRequestModel { Mobile = "9876543210" }, Ct);

        [Test]
        public async Task OnDevelopment_WithTheSettingOn_TheCodeIsReturned_EvenWhenWhatsAppCannotDeliverIt()
        {
            var config = Config("true", "Development");

            var res = await Send(config);

            Assert.That(res.Success, Is.True);
            Assert.That(res.DevCode, Does.Match("^[0-9]{6}$"));
            // and it is the real code: it signs the person in
            var verify = await new ContributorOtpVerifyHandler(_context, config).Handle(
                new ContributorOtpVerifyRequestModel { Mobile = "9876543210", Code = res.DevCode, Role = "WRITER" }, Ct);
            Assert.That(verify.Success, Is.True, verify.Message);
        }

        [TestCase("true", "Production")]
        [TestCase("true", null)]
        [TestCase("false", "Development")]
        [TestCase(null, "Development")]
        public async Task AnywhereElse_NoCodeIsReturned_AndAFailedWhatsAppSendStillFails(string? setting, string? environment)
        {
            var res = await Send(Config(setting, environment));

            Assert.That(res.DevCode, Is.Null);
            Assert.That(res.Success, Is.False);
            Assert.That(res.StatusCode, Is.EqualTo(502));
        }

        [Test]
        public async Task OnProduction_WhenWhatsAppDelivers_TheResponseHasNoCode()
        {
            _whatsApp.Setup(w => w.SendOtpAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(true);

            var res = await Send(Config("true", "Production"));

            Assert.That(res.Success, Is.True);
            Assert.That(res.DevCode, Is.Null);
        }
    }
}
