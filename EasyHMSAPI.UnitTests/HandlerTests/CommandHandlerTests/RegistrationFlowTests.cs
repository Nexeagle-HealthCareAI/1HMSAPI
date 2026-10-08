using System;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Api.Controllers;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModel;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    // Registration became: role -> mobile OTP -> hospital details + map pin -> name / email / password. These cover the server side of the
    // two additions (the admin's name on the set-password step, the hospital's GPS position) and the ownership checks around them.
    [TestFixture]
    public class RegistrationFlowTests
    {
        private AppDbContext _context = null!;
        private Mock<IMaskingService> _masking = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _masking = new Mock<IMaskingService>();
            _masking.Setup(m => m.IsMaskingEnabled()).Returns(false);
            _masking.Setup(m => m.Mask(It.IsAny<string>())).Returns((string s) => s);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        // ---------------------------------------------------------------- name on the set-password step

        private (User User, UserProfile Profile) SeedNewRegistrant(string profileName = "")
        {
            var user = TestDataFactory.SeedUser(_context, email: "placeholder@test.com", password: "oldPassword");
            var profile = new UserProfile { UserProfileID = Guid.NewGuid(), UserID = user.UserID, FullName = profileName, UserStatusId = 1, EmployeeID = "EMP9" };
            _context.UserProfiles.Add(profile);
            _context.SaveChanges();
            return (user, profile);
        }

        private Task<SetOrResetPasswordResponseModel> SetPassword(Guid userId, string? fullName, string email = "admin@hospital.com") =>
            new SetOrResetPasswordHandler(_context, _masking.Object).Handle(new SetOrResetPasswordRequestModel
            {
                UserId = userId, Scope = "set-password", Email = email, Password = "N3wPassw0rd!", FullName = fullName,
            }, CancellationToken.None);

        [Test]
        public async Task SetPassword_WithName_SavesItOnTheProfile_Trimmed()
        {
            var (user, profile) = SeedNewRegistrant();

            var response = await SetPassword(user.UserID, "  Dr. Anita Sharma  ");

            Assert.That(response.Success, Is.True, response.Message);
            _context.Entry(profile).Reload();
            Assert.That(profile.FullName, Is.EqualTo("Dr. Anita Sharma"));
        }

        [Test]
        public async Task SetPassword_WithoutName_WhileTheProfileHasNone_IsRefused_AndSavesNothing()
        {
            var (user, profile) = SeedNewRegistrant();

            var response = await SetPassword(user.UserID, null);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("name is required"));
            _context.Entry(user).Reload();
            Assert.That(user.Email, Is.EqualTo("placeholder@test.com"), "the email must not change when the step is refused");
        }

        [TestCase("A")]
        [TestCase("")]
        [TestCase("   ")]
        public async Task SetPassword_ATooShortName_IsRefused(string name)
        {
            var (user, _) = SeedNewRegistrant();
            var response = await SetPassword(user.UserID, name);
            Assert.That(response.Success, Is.False);
        }

        [Test]
        public async Task SetPassword_ANameOver100Characters_IsRefused()
        {
            var (user, _) = SeedNewRegistrant();
            var response = await SetPassword(user.UserID, new string('x', 101));
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("2 to 100"));
        }

        [Test]
        public async Task SetPassword_ForSomeoneWhoAlreadyHasAName_NeedsNoNewOne()
        {
            var (user, profile) = SeedNewRegistrant(profileName: "Existing Name");

            var response = await SetPassword(user.UserID, null);

            Assert.That(response.Success, Is.True, response.Message);
            _context.Entry(profile).Reload();
            Assert.That(profile.FullName, Is.EqualTo("Existing Name"));
        }

        // ---------------------------------------------------------------- hospital GPS position

        private HospitalRegisterHandler RegisterHandler()
        {
            var config = new Mock<IConfiguration>();
            return new HospitalRegisterHandler(_context, new Mock<IHttpClientFactory>().Object, config.Object, Mock.Of<ILogger<HospitalRegisterHandler>>());
        }

        private HospitalRegisterRequestModel Request(Guid userId, decimal? lat, decimal? lng) => new()
        {
            UserId = userId, Name = "City Care", Type = "Hospital", Email = "admin@hospital.com", Contact = "9876543210", Location = "MG Road",
            City = "Bengaluru", State = "Karnataka", Country = "India", Pincode = "560001", RegistrationNumber = "", Latitude = lat, Longitude = lng,
        };

        [Test]
        public async Task Register_StoresThePinnedPosition()
        {
            var (user, _) = SeedNewRegistrant("Dr A");

            var response = await RegisterHandler().Handle(Request(user.UserID, 12.971599m, 77.594566m), CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            var hospital = _context.Hospitals.Single();
            Assert.That(hospital.Latitude, Is.EqualTo(12.971599m));
            Assert.That(hospital.Longitude, Is.EqualTo(77.594566m));
        }

        [Test]
        public async Task Register_WithoutAPosition_StillWorks()
        {
            var (user, _) = SeedNewRegistrant("Dr A");
            var response = await RegisterHandler().Handle(Request(user.UserID, null, null), CancellationToken.None);
            Assert.That(response.Success, Is.True, response.Message);
            Assert.That(_context.Hospitals.Single().Latitude, Is.Null);
        }

        [TestCase(12.97, null)]
        [TestCase(null, 77.59)]
        [TestCase(91, 77.59)]
        [TestCase(-91, 77.59)]
        [TestCase(12.97, 181)]
        [TestCase(12.97, -181)]
        public async Task Register_AnInvalidOrHalfPosition_IsRefused_AndNothingIsCreated(double? lat, double? lng)
        {
            var (user, _) = SeedNewRegistrant("Dr A");

            var response = await RegisterHandler().Handle(Request(user.UserID, (decimal?)lat, (decimal?)lng), CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("latitude"));
            Assert.That(_context.Hospitals.Any(), Is.False);
        }

        // ---------------------------------------------------------------- a hospital / password can only be set up for yourself

        private static ControllerContext ContextFor(Guid? userId)
        {
            var http = new DefaultHttpContext();
            if (userId.HasValue)
                http.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("userId", userId.Value.ToString()) }, "test"));
            return new ControllerContext { HttpContext = http };
        }

        [Test]
        public async Task RegisterHospital_ForAnotherUsersAccount_IsForbidden_AndNeverReachesTheHandler()
        {
            var mediator = new Mock<IMediator>();
            var controller = new HospitalsController(mediator.Object, Mock.Of<ILogger<HospitalsController>>()) { ControllerContext = ContextFor(Guid.NewGuid()) };

            var result = await controller.RegisterHospital(Request(Guid.NewGuid(), 12.9m, 77.5m));

            Assert.That((result.Result as ObjectResult)?.StatusCode, Is.EqualTo(403));
            mediator.Verify(m => m.Send(It.IsAny<HospitalRegisterRequestModel>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task RegisterHospital_WithoutAResolvableUser_IsUnauthorized()
        {
            var mediator = new Mock<IMediator>();
            var controller = new HospitalsController(mediator.Object, Mock.Of<ILogger<HospitalsController>>()) { ControllerContext = ContextFor(null) };

            var result = await controller.RegisterHospital(Request(Guid.NewGuid(), null, null));

            Assert.That(result.Result, Is.InstanceOf<UnauthorizedObjectResult>());
            mediator.Verify(m => m.Send(It.IsAny<HospitalRegisterRequestModel>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task SetPassword_AlwaysActsOnTheCallersOwnAccount_NeverAClientSuppliedUserId()
        {
            var caller = Guid.NewGuid();
            SetOrResetPasswordRequestModel? sent = null;
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<SetOrResetPasswordRequestModel>(), It.IsAny<CancellationToken>()))
                .Callback<IRequest<SetOrResetPasswordResponseModel>, CancellationToken>((r, _) => sent = (SetOrResetPasswordRequestModel)r)
                .ReturnsAsync(new SetOrResetPasswordResponseModel { Success = true });
            var controller = new AuthServicesController(mediator.Object, Mock.Of<ILogger<AuthServicesController>>(), Mock.Of<IMagicLinkService>(), Mock.Of<IConfiguration>())
            { ControllerContext = ContextFor(caller) };

            await controller.SetOrResetPassword("set-password", new SetOrResetPasswordRequestModel { UserId = Guid.NewGuid(), Email = "a@b.com", Password = "x" });

            Assert.That(sent, Is.Not.Null);
            Assert.That(sent!.UserId, Is.EqualTo(caller), "another user's id in the body must be ignored");
        }
    }
}
