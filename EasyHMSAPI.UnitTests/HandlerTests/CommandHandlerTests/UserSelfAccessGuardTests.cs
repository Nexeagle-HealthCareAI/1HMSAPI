using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Common;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    /// <summary>
    /// Regression tests for the user-profile account-takeover hole: update-user-details, get-user-details
    /// and the profile-picture endpoints used to act on whatever user id the client sent. They must now be
    /// self-only (or admin_panel at a shared hospital for the read/picture paths).
    /// </summary>
    [TestFixture]
    public class UserSelfAccessGuardTests
    {
        private AppDbContext _context = null!;
        private Mock<IBlobStorageService> _blob = null!;
        private IConfiguration _config = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _blob = new Mock<IBlobStorageService>();
            var cfg = new Mock<IConfiguration>();
            cfg.SetupGet(x => x["BlobStorage:ProfilePhotosContainer"]).Returns("profile-photos");
            _config = cfg.Object;
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private User SeedUserWithProfile(string email, string phone)
        {
            var user = TestDataFactory.SeedUser(_context, email: email, phone: phone);
            _context.UserProfiles.Add(new UserProfile { UserProfileID = Guid.NewGuid(), UserID = user.UserID, FullName = "Victim" });
            _context.SaveChanges();
            return user;
        }

        [Test]
        public async Task UpdateDetails_StrangerCannotChangeVictimMobileOrEmail()
        {
            var victim = SeedUserWithProfile("victim@example.com", "9000000001");
            var attacker = SeedUserWithProfile("attacker@example.com", "9000000002");
            var handler = new UserProfileUpdateHandler(_context);

            var response = await handler.Handle(new UserProfileUpdateRequestModel
            {
                UserId = victim.UserID,
                CallerUserId = attacker.UserID,
                MobileNumber = "9999999999",
                Email = "evil@example.com",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Forbidden, Is.True);
            var reloaded = await _context.Users.AsNoTracking().FirstAsync(u => u.UserID == victim.UserID);
            Assert.That(reloaded.MobileNumber, Is.EqualTo("9000000001"));
            Assert.That(reloaded.Email, Is.EqualTo("victim@example.com"));
        }

        [Test]
        public async Task UpdateDetails_MissingCaller_IsForbidden()
        {
            var victim = SeedUserWithProfile("victim@example.com", "9000000001");
            var handler = new UserProfileUpdateHandler(_context);

            var response = await handler.Handle(new UserProfileUpdateRequestModel { UserId = victim.UserID, MobileNumber = "9999999999" }, CancellationToken.None);

            Assert.That(response.Forbidden, Is.True);
        }

        [Test]
        public async Task UpdateDetails_SameHospitalAdminCannotUseThisEndpoint()
        {
            var hospitalId = Guid.NewGuid();
            var admin = HrAuthSeed.SeedMember(_context, hospitalId, "admin_panel");
            var member = SeedUserWithProfile("member@example.com", "9000000003");
            _context.HospitalUsers.Add(new HospitalUser { HospitalUserID = Guid.NewGuid(), HospitalID = hospitalId, UserID = member.UserID });
            _context.SaveChanges();
            var handler = new UserProfileUpdateHandler(_context);

            var response = await handler.Handle(new UserProfileUpdateRequestModel { UserId = member.UserID, CallerUserId = admin, FullName = "Changed" }, CancellationToken.None);

            Assert.That(response.Forbidden, Is.True, "admin edits of members go through admin/users/update");
        }

        [Test]
        public async Task UpdateDetails_SelfCannotTakeAnotherUsersMobileOrEmail()
        {
            var a = SeedUserWithProfile("a@example.com", "9000000001");
            var b = SeedUserWithProfile("b@example.com", "9000000002");
            var handler = new UserProfileUpdateHandler(_context);

            var mobile = await handler.Handle(new UserProfileUpdateRequestModel { UserId = a.UserID, CallerUserId = a.UserID, MobileNumber = "9000000002" }, CancellationToken.None);
            var email = await handler.Handle(new UserProfileUpdateRequestModel { UserId = a.UserID, CallerUserId = a.UserID, Email = "b@example.com" }, CancellationToken.None);

            Assert.That(mobile.Success, Is.False);
            Assert.That(mobile.Message, Does.Contain("mobile"));
            Assert.That(email.Success, Is.False);
            Assert.That(email.Message, Does.Contain("e-mail"));
        }

        [Test]
        public async Task GetDetails_StrangerForbidden_SelfAllowed_SameHospitalAdminAllowedWithoutLoginSecurity()
        {
            var hospitalId = Guid.NewGuid();
            var admin = HrAuthSeed.SeedMember(_context, hospitalId, "admin_panel");
            var member = SeedUserWithProfile("member@example.com", "9000000003");
            _context.HospitalUsers.Add(new HospitalUser { HospitalUserID = Guid.NewGuid(), HospitalID = hospitalId, UserID = member.UserID });
            // SeedUser already created the user's auth row; give it a last-login IP to assert on.
            _context.UserAuths.First(a => a.UserID == member.UserID).LastLoginIP = "10.0.0.1";
            var stranger = SeedUserWithProfile("stranger@example.com", "9000000004");
            _context.SaveChanges();
            var handler = new UserSearchHandler(_context);

            var asStranger = await handler.Handle(new UserSearchRequestModel { UserId = member.UserID, CallerUserId = stranger.UserID }, CancellationToken.None);
            var asSelf = await handler.Handle(new UserSearchRequestModel { UserId = member.UserID, CallerUserId = member.UserID }, CancellationToken.None);
            var asAdmin = await handler.Handle(new UserSearchRequestModel { UserId = member.UserID, CallerUserId = admin }, CancellationToken.None);

            Assert.That(asStranger!.Forbidden, Is.True);
            Assert.That(asStranger.MobileNumber, Is.Null.Or.Empty);
            Assert.That(asSelf!.Forbidden, Is.False);
            Assert.That(asSelf.UserAuth?.LastLoginIP, Is.EqualTo("10.0.0.1"));
            Assert.That(asAdmin!.Forbidden, Is.False);
            Assert.That(asAdmin.UserAuth, Is.Null, "login-security details are self-only");
        }

        [Test]
        public async Task GetDetails_AdminOfAnotherHospitalIsForbidden()
        {
            var admin = HrAuthSeed.SeedMember(_context, Guid.NewGuid(), "admin_panel");
            var member = SeedUserWithProfile("member@example.com", "9000000003");
            _context.HospitalUsers.Add(new HospitalUser { HospitalUserID = Guid.NewGuid(), HospitalID = Guid.NewGuid(), UserID = member.UserID });
            _context.SaveChanges();
            var handler = new UserSearchHandler(_context);

            var response = await handler.Handle(new UserSearchRequestModel { UserId = member.UserID, CallerUserId = admin }, CancellationToken.None);

            Assert.That(response!.Forbidden, Is.True);
        }

        [Test]
        public async Task DeletePicture_StrangerForbidden_BlobUntouched()
        {
            var victim = SeedUserWithProfile("victim@example.com", "9000000001");
            var attacker = SeedUserWithProfile("attacker@example.com", "9000000002");
            var handler = new DeleteProfilePictureHandler(_config, _blob.Object, _context);

            var response = await handler.Handle(new DeleteProfilePictureRequestModel { UserId = victim.UserID, CallerUserId = attacker.UserID }, CancellationToken.None);

            Assert.That(response.Forbidden, Is.True);
            _blob.Verify(b => b.DeleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task UploadPicture_StrangerForbidden_EvenWithClaimedHospitalId()
        {
            var victim = SeedUserWithProfile("victim@example.com", "9000000001");
            var attacker = SeedUserWithProfile("attacker@example.com", "9000000002");
            var handler = new UploadImageCommandHandler(_config, _blob.Object, _context);

            var response = await handler.Handle(new UploadProfilePictureRequestModel
            {
                UserId = victim.UserID,
                CallerUserId = attacker.UserID,
                HospitalId = Guid.NewGuid(),
            }, CancellationToken.None);

            Assert.That(response.Forbidden, Is.True);
            _blob.Verify(b => b.UploadAsync(It.IsAny<string>(), It.IsAny<Microsoft.AspNetCore.Http.IFormFile>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task GetPicture_ColleagueAllowed_StrangerForbidden()
        {
            var hospitalId = Guid.NewGuid();
            var colleague = HrAuthSeed.SeedMember(_context, hospitalId);
            var member = SeedUserWithProfile("member@example.com", "9000000003");
            _context.HospitalUsers.Add(new HospitalUser { HospitalUserID = Guid.NewGuid(), HospitalID = hospitalId, UserID = member.UserID });
            var stranger = SeedUserWithProfile("stranger@example.com", "9000000004");
            _context.SaveChanges();
            var handler = new GetProfilePictureHandler(_config, _blob.Object, _context);

            var asColleague = await handler.Handle(new GetProfilePictureRequestModel { UserId = member.UserID, CallerUserId = colleague }, CancellationToken.None);
            var asStranger = await handler.Handle(new GetProfilePictureRequestModel { UserId = member.UserID, CallerUserId = stranger.UserID }, CancellationToken.None);

            Assert.That(asColleague.Forbidden, Is.False);
            Assert.That(asStranger.Forbidden, Is.True);
        }

        [Test]
        public async Task CallerGuards_CanAccessUser_RequiresAdminPermissionAtSharedHospital()
        {
            var hospitalId = Guid.NewGuid();
            var plainMember = HrAuthSeed.SeedMember(_context, hospitalId);
            var admin = HrAuthSeed.SeedMember(_context, hospitalId, "admin_panel");
            var target = HrAuthSeed.SeedMember(_context, hospitalId);

            Assert.That(await CallerGuards.CanAccessUserAsync(_context, target, target, CancellationToken.None), Is.True);
            Assert.That(await CallerGuards.CanAccessUserAsync(_context, admin, target, CancellationToken.None), Is.True);
            Assert.That(await CallerGuards.CanAccessUserAsync(_context, plainMember, target, CancellationToken.None), Is.False);
            Assert.That(await CallerGuards.CanAccessUserAsync(_context, null, target, CancellationToken.None), Is.False);
        }
    }
}
