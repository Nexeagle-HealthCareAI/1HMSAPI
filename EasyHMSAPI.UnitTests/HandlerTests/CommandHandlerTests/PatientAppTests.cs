using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    /// <summary>The Doctor Dekho app's quick-booking preferences and installs, keyed by the verified mobile.</summary>
    [TestFixture]
    public class PatientAppTests
    {
        private AppDbContext _context = null!;
        private static CancellationToken Ct => CancellationToken.None;
        private const string Mobile = "9876543210";

        [SetUp]
        public void SetUp() => _context = InMemoryDbContextFactory.CreateContext();

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private Task<EasyHMSAPI.Application.ResponseModels.QueryResponseModels.PatientAppProfileResponseModel> Save(Action<SavePatientAppProfileRequestModel> set, string mobile = Mobile)
        {
            var r = new SavePatientAppProfileRequestModel { Mobile = mobile };
            set(r);
            return new SavePatientAppProfileHandler(_context).Handle(r, Ct);
        }

        private Task<EasyHMSAPI.Application.ResponseModels.QueryResponseModels.PatientAppProfileResponseModel> Read(string mobile = Mobile) =>
            new GetPatientAppProfileHandler(_context).Handle(new GetPatientAppProfileRequestModel { Mobile = mobile }, Ct);

        [Test]
        public async Task ANewPatientGetsDefaults_NotAnError()
        {
            var read = await Read();
            Assert.That(read.Success, Is.True);
            Assert.That((read.OnboardingCompleted, read.HasShortProfile, read.RemindersOptIn), Is.EqualTo((false, false, false)));
        }

        [Test]
        public async Task SavingOnlyChangesTheFieldsSent()
        {
            await Save(r => { r.PreferredLanguage = "HI"; r.HomeCity = "Purnea"; r.PreferredTimeWindow = "morning"; });
            await Save(r => r.LastDoctorId = Guid.NewGuid());

            var read = await Read();
            Assert.That((read.PreferredLanguage, read.HomeCity, read.PreferredTimeWindow), Is.EqualTo(("hi", "Purnea", "MORNING")));
            Assert.That(read.LastDoctorId, Is.Not.Null);
            Assert.That(read.LastBookedAt, Is.Not.Null);
        }

        [Test]
        public async Task UnknownLanguageOrWindowIsRejected()
        {
            Assert.That((await Save(r => r.PreferredLanguage = "fr")).StatusCode, Is.EqualTo(400));
            Assert.That((await Save(r => r.PreferredTimeWindow = "LUNCH")).StatusCode, Is.EqualTo(400));
            Assert.That((await Save(r => { r.HomeLatitude = 25.77m; })).StatusCode, Is.EqualTo(400), "latitude without longitude");
        }

        [Test]
        public async Task DefaultMemberMustBelongToThisAccount()
        {
            var mine = new PatientFamilyMember { MemberId = Guid.NewGuid(), OwnerMobile = Mobile, FirstName = "Aarav", Relation = "Son", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            var theirs = new PatientFamilyMember { MemberId = Guid.NewGuid(), OwnerMobile = "9000000000", FirstName = "Riya", Relation = "Daughter", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            _context.PatientFamilyMembers.AddRange(mine, theirs);
            await _context.SaveChangesAsync();

            Assert.That((await Save(r => r.DefaultMemberId = theirs.MemberId)).StatusCode, Is.EqualTo(404));
            Assert.That((await Save(r => r.DefaultMemberId = mine.MemberId)).DefaultMemberId, Is.EqualTo(mine.MemberId));
            Assert.That((await Save(r => r.ClearDefaultMember = true)).DefaultMemberId, Is.Null);
        }

        [Test]
        public async Task ShortProfileIsCompleteOnceNameAgeAndSexAreSaved()
        {
            _context.PatientAccountProfiles.Add(new PatientAccountProfile { Mobile = Mobile, FullName = "Aquib Khan", FirstName = "Aquib", UpdatedAt = DateTime.UtcNow });
            await _context.SaveChangesAsync();
            Assert.That((await Read()).HasShortProfile, Is.False);

            var row = _context.PatientAccountProfiles.Single(p => p.Mobile == Mobile);
            row.Age = 32; row.AgeUnit = "Y"; row.Sex = "Male";
            await _context.SaveChangesAsync();
            Assert.That((await Read()).HasShortProfile, Is.True);
        }

        [Test]
        public async Task OnboardingAndReminderConsentAreStamped()
        {
            var saved = await Save(r => { r.OnboardingCompleted = true; r.RemindersOptIn = true; });
            Assert.That((saved.OnboardingCompleted, saved.RemindersOptIn), Is.EqualTo((true, true)));
            Assert.That(_context.PatientAppProfiles.Single().RemindersOptInAt, Is.Not.Null);
        }

        [Test]
        public async Task RegisteringTheSameInstallTwiceUpdatesOneRow_AndMovesThePushToken()
        {
            var handler = new RegisterPatientAppDeviceHandler(_context);
            await handler.Handle(new RegisterPatientAppDeviceRequestModel { Mobile = "9000000000", InstallationId = "inst-1", Platform = "android", PushToken = "ExponentPushToken[abc]" }, Ct);
            await handler.Handle(new RegisterPatientAppDeviceRequestModel { Mobile = Mobile, InstallationId = "inst-1", Platform = "Android", AppVersion = "1.0.0" }, Ct);
            var again = await handler.Handle(new RegisterPatientAppDeviceRequestModel { Mobile = Mobile, InstallationId = "inst-1", Platform = "android", AppVersion = "1.0.1", PushToken = "ExponentPushToken[abc]" }, Ct);

            Assert.That(again.Success, Is.True, again.Message);
            var mine = _context.PatientAppDevices.Where(d => d.Mobile == Mobile).ToList();
            Assert.That(mine.Count, Is.EqualTo(1));
            Assert.That((mine[0].AppVersion, mine[0].IsActive, mine[0].PushToken), Is.EqualTo(("1.0.1", true, "ExponentPushToken[abc]")));
            Assert.That(_context.PatientAppDevices.Single(d => d.Mobile == "9000000000").IsActive, Is.False, "the other account no longer gets this phone's pushes");
        }

        [Test]
        public async Task DeviceNeedsAValidPlatform_AndSignOutDeactivatesIt()
        {
            var handler = new RegisterPatientAppDeviceHandler(_context);
            Assert.That((await handler.Handle(new RegisterPatientAppDeviceRequestModel { Mobile = Mobile, InstallationId = "x", Platform = "windows" }, Ct)).StatusCode, Is.EqualTo(400));

            await handler.Handle(new RegisterPatientAppDeviceRequestModel { Mobile = Mobile, InstallationId = "inst-2", Platform = "ios" }, Ct);
            var off = await new DeactivatePatientAppDeviceHandler(_context).Handle(new DeactivatePatientAppDeviceRequestModel { Mobile = Mobile, InstallationId = "inst-2" }, Ct);
            Assert.That(off.Success, Is.True);
            Assert.That(_context.PatientAppDevices.Single().IsActive, Is.False);

            var unknown = await new DeactivatePatientAppDeviceHandler(_context).Handle(new DeactivatePatientAppDeviceRequestModel { Mobile = Mobile, InstallationId = "never-seen" }, Ct);
            Assert.That(unknown.Success, Is.True);
        }
    }
}
