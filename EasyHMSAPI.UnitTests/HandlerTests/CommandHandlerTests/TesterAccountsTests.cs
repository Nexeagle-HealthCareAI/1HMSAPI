using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    /// <summary>Tester numbers (StaffAuth:TesterMobiles): only on Development, code on the page, nothing sent.</summary>
    [TestFixture]
    public class TesterAccountsTests
    {
        private const string Tester = "9000000001";
        private AppDbContext _context = null!;
        private Mock<IEmailService> _email = null!;
        private Mock<IWhatsAppMessagingService> _whatsApp = null!;
        private Mock<IMaskingService> _masking = null!;

        public static IConfiguration Config(string environment, string? mobiles = Tester, string? resetKey = "key-123") =>
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = environment,
                ["StaffAuth:TesterMobiles"] = mobiles,
                ["StaffAuth:TesterResetKey"] = resetKey,
            }).Build();

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _email = new Mock<IEmailService>();
            _whatsApp = new Mock<IWhatsAppMessagingService>();
            _masking = new Mock<IMaskingService>();
            _masking.Setup(m => m.Mask(It.IsAny<string>())).Returns((string s) => s);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        [Test]
        public void IsTester_OnlyOnDevelopment_AndOnlyForListedNumbers()
        {
            Assert.That(TesterAccounts.IsTester(Config("Development"), Tester), Is.True);
            Assert.That(TesterAccounts.IsTester(Config("Development"), "+91 90000 00001"), Is.True);
            Assert.That(TesterAccounts.IsTester(Config("Development"), "9876543210"), Is.False);
            Assert.That(TesterAccounts.IsTester(Config("Production"), Tester), Is.False);
            Assert.That(TesterAccounts.IsTester(Config("Staging"), Tester), Is.False);
            Assert.That(TesterAccounts.IsTester(Config("Development", mobiles: ""), Tester), Is.False);
            Assert.That(TesterAccounts.IsTester(Config("Development", mobiles: "<set-via-env>"), Tester), Is.False);
        }

        [Test]
        public void Mobiles_AcceptsSeveralSeparators_AndDropsWhatIsNotTenDigits()
        {
            var mobiles = TesterAccounts.Mobiles(Config("Development", mobiles: "9000000001, 9000000002;919000000003 12345"));
            Assert.That(mobiles, Is.EquivalentTo(new[] { "9000000001", "9000000002", "9000000003" }));
        }

        [Test]
        public void ResetKey_IsOffOutsideDevelopment_AndWhenUnset()
        {
            Assert.That(TesterAccounts.ResetKey(Config("Development")), Is.EqualTo("key-123"));
            Assert.That(TesterAccounts.ResetKey(Config("Production")), Is.Null);
            Assert.That(TesterAccounts.ResetKey(Config("Development", resetKey: "")), Is.Null);
            Assert.That(TesterAccounts.ResetKey(Config("Development", resetKey: "<set-via-env-StaffAuth-TesterResetKey>")), Is.Null);
        }

        private Task<EasyHMSAPI.Application.ResponseModels.CommandResponseModels.OtpSendResponseModel> Send(IConfiguration config, string mobile) =>
            new OtpSendHandler(_context, _email.Object, _whatsApp.Object, config, _masking.Object)
                .Handle(new OtpSendRequestModel { MobileNumber = mobile }, CancellationToken.None);

        [Test]
        public async Task TesterOnDevelopment_GetsTheCodeOnThePage_AndNothingIsSent()
        {
            var user = TestDataFactory.SeedUser(_context, phone: Tester, email: "tester@example.com");

            var r = await Send(Config("Development"), Tester);

            Assert.That(r.Success, Is.True);
            Assert.That(r.DevCode, Has.Length.EqualTo(6));
            Assert.That(r.IsWhatsappSent, Is.False);
            Assert.That(r.IsEmailSent, Is.False);
            _whatsApp.Verify(w => w.SendOtpAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _email.Verify(e => e.SendOtpEmailAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            var auth = await _context.UserAuths.FirstAsync(a => a.UserID == user.UserID);
            Assert.That(auth.Otp, Is.EqualTo(r.DevCode), "the code shown is the code stored, so verifying it works");
        }

        [Test]
        public async Task TesterNumberOnProduction_IsTreatedLikeAnyone_NoCodeInTheReply()
        {
            TestDataFactory.SeedUser(_context, phone: Tester, email: "tester@example.com");
            _whatsApp.Setup(w => w.SendOtpAsync(Tester, It.IsAny<string>())).ReturnsAsync(true);

            var r = await Send(Config("Production"), Tester);

            Assert.That(r.Success, Is.True);
            Assert.That(r.DevCode, Is.Null);
            Assert.That(r.IsWhatsappSent, Is.True);
            _whatsApp.Verify(w => w.SendOtpAsync(Tester, It.IsAny<string>()), Times.Once);
        }

        [Test]
        public async Task OrdinaryNumberOnDevelopment_StillGoesToWhatsApp_NoCodeInTheReply()
        {
            const string real = "9876543210";
            TestDataFactory.SeedUser(_context, phone: real, email: "real@example.com");
            _whatsApp.Setup(w => w.SendOtpAsync(real, It.IsAny<string>())).ReturnsAsync(true);

            var r = await Send(Config("Development"), real);

            Assert.That(r.DevCode, Is.Null);
            _whatsApp.Verify(w => w.SendOtpAsync(real, It.IsAny<string>()), Times.Once);
        }
    }
}
