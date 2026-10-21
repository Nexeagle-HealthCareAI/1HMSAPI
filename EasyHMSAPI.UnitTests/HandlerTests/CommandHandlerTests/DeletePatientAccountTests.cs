using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    /// <summary>Patient-initiated account deletion: removes what the account owns, keeps the auth row (sessions revoked), leaves other patients alone.</summary>
    [TestFixture]
    public class DeletePatientAccountTests
    {
        private AppDbContext _context = null!;
        private Mock<IBlobStorageService> _blob = null!;
        private static CancellationToken Ct => CancellationToken.None;
        private const string Mine = "9876543210";
        private const string Other = "9000000000";

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _blob = new Mock<IBlobStorageService>();
            _blob.Setup(b => b.DeleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private DeletePatientAccountHandler Handler() => new(_context, _blob.Object, new ConfigurationBuilder().Build());

        private async Task SeedAccount(string mobile)
        {
            var now = DateTime.UtcNow;
            var member = new PatientFamilyMember { MemberId = Guid.NewGuid(), OwnerMobile = mobile, FirstName = "Aarav", Relation = "Son", CreatedAt = now, UpdatedAt = now };
            _context.PatientFamilyMembers.Add(member);
            _context.PatientAccountProfiles.Add(new PatientAccountProfile { Mobile = mobile, FullName = "Aquib Khan", FirstName = "Aquib", UpdatedAt = now });
            _context.PatientAppProfiles.Add(new PatientAppProfile { Mobile = mobile, PreferredLanguage = "hi", CreatedAt = now, UpdatedAt = now });
            _context.PatientAppDevices.Add(new PatientAppDevice { DeviceId = Guid.NewGuid(), Mobile = mobile, InstallationId = "install-" + mobile, Platform = "android", IsActive = true, FirstSeenAt = now, LastSeenAt = now });
            _context.PatientHealthLockerDocuments.AddRange(
                new PatientHealthLockerDocument { DocumentId = Guid.NewGuid(), Mobile = mobile, FileName = "rx.pdf" },
                new PatientHealthLockerDocument { DocumentId = Guid.NewGuid(), Mobile = mobile, MemberId = member.MemberId, FileName = "lab.pdf" });
            _context.PublicPatientAuths.Add(new PublicPatientAuth { Mobile = mobile, Otp = "123456", SessionEpoch = 3 });
            await _context.SaveChangesAsync();
        }

        [Test]
        public async Task DeletesEverythingTheAccountOwns_AndRevokesSessions()
        {
            await SeedAccount(Mine);

            var r = await Handler().Handle(new DeletePatientAccountRequestModel { Mobile = Mine }, Ct);

            Assert.That(r.Success, Is.True);
            Assert.That((r.DocumentsDeleted, r.FamilyMembersDeleted, r.DocumentsPending), Is.EqualTo((2, 1, 0)));
            Assert.That(await _context.PatientHealthLockerDocuments.AnyAsync(d => d.Mobile == Mine), Is.False);
            Assert.That(await _context.PatientFamilyMembers.AnyAsync(m => m.OwnerMobile == Mine), Is.False);
            Assert.That(await _context.PatientAccountProfiles.AnyAsync(p => p.Mobile == Mine), Is.False);
            Assert.That(await _context.PatientAppProfiles.AnyAsync(p => p.Mobile == Mine), Is.False);
            Assert.That(await _context.PatientAppDevices.AnyAsync(d => d.Mobile == Mine), Is.False);

            // The auth row stays so SessionEpoch keeps rising: deleting it would let old tokens (epoch 0..3) validate again later.
            var auth = await _context.PublicPatientAuths.SingleAsync(a => a.Mobile == Mine);
            Assert.That(auth.SessionEpoch, Is.EqualTo(4));
            Assert.That(auth.Otp, Is.Null);
            _blob.Verify(b => b.DeleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Test]
        public async Task OtherPatientsAreUntouched()
        {
            await SeedAccount(Mine);
            await SeedAccount(Other);

            await Handler().Handle(new DeletePatientAccountRequestModel { Mobile = Mine }, Ct);

            Assert.That(await _context.PatientHealthLockerDocuments.CountAsync(d => d.Mobile == Other), Is.EqualTo(2));
            Assert.That(await _context.PatientFamilyMembers.CountAsync(m => m.OwnerMobile == Other), Is.EqualTo(1));
            Assert.That(await _context.PatientAccountProfiles.AnyAsync(p => p.Mobile == Other), Is.True);
            Assert.That(await _context.PatientAppProfiles.AnyAsync(p => p.Mobile == Other), Is.True);
            Assert.That(await _context.PatientAppDevices.AnyAsync(d => d.Mobile == Other), Is.True);
            Assert.That((await _context.PublicPatientAuths.SingleAsync(a => a.Mobile == Other)).SessionEpoch, Is.EqualTo(3));
        }

        [Test]
        public async Task AFileThatWillNotDelete_StopsBeforeTheProfile_AndARetryFinishes()
        {
            await SeedAccount(Mine);
            var stuck = await _context.PatientHealthLockerDocuments.FirstAsync(d => d.FileName == "lab.pdf");
            _blob.Setup(b => b.DeleteAsync(stuck.DocumentId.ToString(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

            var first = await Handler().Handle(new DeletePatientAccountRequestModel { Mobile = Mine }, Ct);

            Assert.That((first.Success, first.StatusCode, first.DocumentsDeleted, first.DocumentsPending), Is.EqualTo((false, 502, 1, 1)));
            Assert.That(await _context.PatientAccountProfiles.AnyAsync(p => p.Mobile == Mine), Is.True, "nothing else is deleted while a file is pending");
            Assert.That((await _context.PublicPatientAuths.SingleAsync(a => a.Mobile == Mine)).SessionEpoch, Is.EqualTo(3));

            _blob.Setup(b => b.DeleteAsync(stuck.DocumentId.ToString(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            var second = await Handler().Handle(new DeletePatientAccountRequestModel { Mobile = Mine }, Ct);

            Assert.That((second.Success, second.DocumentsDeleted, second.DocumentsPending), Is.EqualTo((true, 1, 0)));
            Assert.That(await _context.PatientAccountProfiles.AnyAsync(p => p.Mobile == Mine), Is.False);
        }

        [Test]
        public async Task ACallWithNoAccountData_StillSucceeds()
        {
            var r = await Handler().Handle(new DeletePatientAccountRequestModel { Mobile = Mine }, Ct);
            Assert.That(r.Success, Is.True);
        }

        [Test]
        public async Task NoMobile_IsRejected()
        {
            var r = await Handler().Handle(new DeletePatientAccountRequestModel { Mobile = "" }, Ct);
            Assert.That((r.Success, r.StatusCode), Is.EqualTo((false, 401)));
        }
    }
}
