using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    /// <summary>"Delete account" removes everything the patient keeps with Doctor Dekho, for that number only, and ends their sessions.</summary>
    [TestFixture]
    public class DeletePatientAccountTests
    {
        private AppDbContext _context = null!;
        private Mock<IBlobStorageService> _blob = null!;
        private const string Mine = "9876543210", Other = "9123456780";

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

        private void SeedAccount(string mobile)
        {
            _context.PublicPatientAuths.Add(new PublicPatientAuth { Mobile = mobile, SessionEpoch = 3 });
            _context.PatientAccountProfiles.Add(new PatientAccountProfile { Mobile = mobile, FullName = "Priya Kumari" });
            _context.PatientAppProfiles.Add(new PatientAppProfile { Mobile = mobile, PreferredLanguage = "hi" });
            _context.PatientAppDevices.Add(new PatientAppDevice { DeviceId = Guid.NewGuid(), Mobile = mobile, InstallationId = Guid.NewGuid().ToString(), Platform = "android", IsActive = true });
            _context.PatientFamilyMembers.Add(new PatientFamilyMember { MemberId = Guid.NewGuid(), OwnerMobile = mobile, FirstName = "Sunita", Relation = "Mother", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            _context.PatientHealthLockerDocuments.Add(new PatientHealthLockerDocument { DocumentId = Guid.NewGuid(), Mobile = mobile, FileName = "rx.jpg", DocumentType = "Prescription" });
            _context.PatientHealthLockerDocuments.Add(new PatientHealthLockerDocument { DocumentId = Guid.NewGuid(), Mobile = mobile, FileName = "report.pdf", DocumentType = "Report" });
            _context.SaveChanges();
        }

        private Task<EasyHMSAPI.Application.ResponseModels.CommandResponseModels.DeletePatientAccountResponseModel> Delete(string mobile = Mine) =>
            new DeletePatientAccountHandler(_context, _blob.Object, new ConfigurationBuilder().Build()).Handle(new DeletePatientAccountRequestModel { Mobile = mobile }, CancellationToken.None);

        [Test]
        public async Task RemovesEverythingForThatNumber_EndsSessions_AndLeavesOtherAccountsAlone()
        {
            SeedAccount(Mine);
            SeedAccount(Other);

            var res = await Delete();

            Assert.That(res.Success, Is.True);
            Assert.That(await _context.PatientAccountProfiles.AnyAsync(p => p.Mobile == Mine), Is.False);
            Assert.That(await _context.PatientAppProfiles.AnyAsync(p => p.Mobile == Mine), Is.False);
            Assert.That(await _context.PatientAppDevices.AnyAsync(d => d.Mobile == Mine), Is.False);
            Assert.That(await _context.PatientFamilyMembers.AnyAsync(m => m.OwnerMobile == Mine), Is.False);
            Assert.That(await _context.PatientHealthLockerDocuments.AnyAsync(d => d.Mobile == Mine), Is.False);
            Assert.That((await _context.PublicPatientAuths.AsNoTracking().SingleAsync(a => a.Mobile == Mine)).SessionEpoch, Is.EqualTo(4));
            _blob.Verify(b => b.DeleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));

            Assert.That(await _context.PatientAccountProfiles.AnyAsync(p => p.Mobile == Other), Is.True);
            Assert.That(await _context.PatientHealthLockerDocuments.CountAsync(d => d.Mobile == Other), Is.EqualTo(2));
            Assert.That((await _context.PublicPatientAuths.AsNoTracking().SingleAsync(a => a.Mobile == Other)).SessionEpoch, Is.EqualTo(3));
        }

        [Test]
        public async Task AFileThatIsAlreadyGone_DoesNotBlockDeletion()
        {
            SeedAccount(Mine);
            _blob.Setup(b => b.DeleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

            var res = await Delete();

            Assert.That(res.Success, Is.True);
            Assert.That(await _context.PatientHealthLockerDocuments.AnyAsync(d => d.Mobile == Mine), Is.False);
        }

        [Test]
        public async Task StorageError_KeepsThatDocumentAndSessions_SoTheCallCanBeRepeated()
        {
            SeedAccount(Mine);
            _blob.SetupSequence(b => b.DeleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("storage down"))
                .ReturnsAsync(true);

            var first = await Delete();

            Assert.That(first.Success, Is.False);
            Assert.That(first.DocumentsPending, Is.EqualTo(1));
            Assert.That(await _context.PatientHealthLockerDocuments.CountAsync(d => d.Mobile == Mine), Is.EqualTo(1));
            Assert.That(await _context.PatientFamilyMembers.AnyAsync(m => m.OwnerMobile == Mine), Is.False);
            Assert.That((await _context.PublicPatientAuths.AsNoTracking().SingleAsync(a => a.Mobile == Mine)).SessionEpoch, Is.EqualTo(3));

            _blob.Setup(b => b.DeleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            var second = await Delete();

            Assert.That(second.Success, Is.True);
            Assert.That(await _context.PatientHealthLockerDocuments.AnyAsync(d => d.Mobile == Mine), Is.False);
            Assert.That((await _context.PublicPatientAuths.AsNoTracking().SingleAsync(a => a.Mobile == Mine)).SessionEpoch, Is.EqualTo(4));
        }
    }
}
