using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Exceptions;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Application.Services.Implementations;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class HealthWikiImageTests
    {
        private static readonly byte[] Jpeg = { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46, 0x49, 0x46, 0, 1, 1, 1, 0, 0x48 };
        private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D, 0x49, 0x48, 0x44, 0x52 };
        private static readonly byte[] WebP = { 0x52, 0x49, 0x46, 0x46, 0x24, 0, 0, 0, 0x57, 0x45, 0x42, 0x50, 0x56, 0x50, 0x38, 0x20 };
        private static readonly byte[] Gif = { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 1, 0, 1, 0, 0, 0 };
        private static readonly byte[] Pdf = { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34, 0x0A, 0x25, 0xE2, 0xE3 };
        private static readonly byte[] Script = System.Text.Encoding.UTF8.GetBytes("<script>alert(1)</script>");

        private AppDbContext _context = null!;
        private Mock<IHealthWikiImageStore> _store = null!;
        private string? _storedKey, _storedType;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _storedKey = null; _storedType = null;
            _store = new Mock<IHealthWikiImageStore>();
            _store.SetupGet(s => s.IsConfigured).Returns(true);
            _store.Setup(s => s.PutAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
                .Callback<string, Stream, string, long, CancellationToken>((k, _, t, _, _) => { _storedKey = k; _storedType = t; })
                .ReturnsAsync((string k, Stream _, string _, long _, CancellationToken _) => "https://nexeagle-healthwiki-dev.example.net/" + k);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private HealthWikiImageService Service => new(_context, _store.Object, NullLogger<HealthWikiImageService>.Instance);

        private static IFormFile File(byte[] bytes, string name = "photo.jpg", string contentType = "image/jpeg", int? padTo = null)
        {
            var data = padTo.HasValue ? bytes.Concat(new byte[Math.Max(0, padTo.Value - bytes.Length)]).ToArray() : bytes;
            return new FormFile(new MemoryStream(data), 0, data.Length, "file", name) { Headers = new HeaderDictionary(), ContentType = contentType };
        }

        // ------------------------------------------------------------------------------------------ the rules

        [Test]
        public void Detect_ReadsTheRealTypeFromTheFirstBytes()
        {
            Assert.That(HealthWikiImageRules.Detect(Jpeg), Is.EqualTo(("image/jpeg", "jpg")));
            Assert.That(HealthWikiImageRules.Detect(Png), Is.EqualTo(("image/png", "png")));
            Assert.That(HealthWikiImageRules.Detect(WebP), Is.EqualTo(("image/webp", "webp")));
            Assert.That(HealthWikiImageRules.Detect(Gif), Is.Null);
            Assert.That(HealthWikiImageRules.Detect(Pdf), Is.Null);
            Assert.That(HealthWikiImageRules.Detect(Script), Is.Null);
            Assert.That(HealthWikiImageRules.Detect(new byte[0]), Is.Null);
            Assert.That(HealthWikiImageRules.Detect(new byte[] { 0xFF, 0xD8 }), Is.Null, "too short to be sure");
        }

        [Test]
        public void Keys_AreRandom_UnderArticles_AndKeepNoUploaderName()
        {
            var a = HealthWikiImageRules.NewKey("png"); var b = HealthWikiImageRules.NewKey("png");
            Assert.That(a, Is.Not.EqualTo(b));
            Assert.That(a, Does.Match("^articles/[0-9a-f]{32}\\.png$"));
            Assert.That(HealthWikiImageRules.PublicUrl("https://x.example.net/", "articles/a.png"), Is.EqualTo("https://x.example.net/articles/a.png"));
        }

        // ------------------------------------------------------------------------------------------ the upload

        [TestCase("jpg")] [TestCase("png")] [TestCase("webp")]
        public async Task AGoodImage_IsStoredWithARandomName_AndRecorded(string kind)
        {
            var bytes = kind == "jpg" ? Jpeg : kind == "png" ? Png : WebP;
            var res = await Service.UploadAsync(File(bytes, name: "My Holiday Photo (final).jpeg"), Guid.NewGuid(), "Dr. Meera", CancellationToken.None);

            Assert.That(res.Success, Is.True, res.Message);
            Assert.That(res.StatusCode, Is.EqualTo(201));
            Assert.That(res.Url, Does.StartWith("https://").And.EndsWith("." + kind));
            Assert.That(_storedKey, Does.Not.Contain("Holiday"), "the uploader's file name is never used");
            var row = _context.HealthArticleImages.Single();
            Assert.That(row.Url, Is.EqualTo(res.Url));
            Assert.That(row.UploadedByName, Is.EqualTo("Dr. Meera"));
            Assert.That(row.SizeBytes, Is.EqualTo(bytes.Length));
        }

        [Test]
        public async Task TheTypeComesFromTheFile_NotFromTheNameOrTheClaimedType()
        {
            var png = await Service.UploadAsync(File(Png, name: "x.jpg", contentType: "image/jpeg"), null, "CMS", CancellationToken.None);
            Assert.That(png.Success, Is.True);
            Assert.That(_storedType, Is.EqualTo("image/png"));
            Assert.That(_storedKey, Does.EndWith(".png"));

            foreach (var bad in new[] { Pdf, Script, Gif })
            {
                var res = await Service.UploadAsync(File(bad, name: "innocent.jpg", contentType: "image/jpeg"), null, "CMS", CancellationToken.None);
                Assert.That(res.StatusCode, Is.EqualTo(400), "a renamed document or script is refused");
            }
            Assert.That(_context.HealthArticleImages.Count(), Is.EqualTo(1));
        }

        [Test]
        public async Task SizeAndPresence_AreChecked()
        {
            var tooBig = await Service.UploadAsync(File(Jpeg, padTo: HealthWikiImageRules.MaxBytes + 1), null, "CMS", CancellationToken.None);
            var exactly = await Service.UploadAsync(File(Jpeg, padTo: HealthWikiImageRules.MaxBytes), null, "CMS", CancellationToken.None);
            var none = await Service.UploadAsync(null, null, "CMS", CancellationToken.None);
            var empty = await Service.UploadAsync(File(new byte[0]), null, "CMS", CancellationToken.None);

            Assert.That(tooBig.StatusCode, Is.EqualTo(400));
            Assert.That(tooBig.Message, Does.Contain("2 MB"));
            Assert.That(exactly.Success, Is.True, "2 MB exactly is allowed");
            Assert.That(none.StatusCode, Is.EqualTo(400));
            Assert.That(empty.StatusCode, Is.EqualTo(400));
        }

        [Test]
        public async Task WhenTheBucketIsNotSetUp_ItSaysSo_AndStoresNothing()
        {
            _store.SetupGet(s => s.IsConfigured).Returns(false);
            var res = await Service.UploadAsync(File(Jpeg), null, "CMS", CancellationToken.None);

            Assert.That(res.StatusCode, Is.EqualTo(503));
            _store.Verify(s => s.PutAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task WhenTheStoreFails_NothingIsRecorded_AndThePersonGetsAPlainMessage()
        {
            _store.Setup(s => s.PutAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("AccessDenied: key is not allowed on this bucket"));

            var res = await Service.UploadAsync(File(Jpeg), null, "CMS", CancellationToken.None);

            Assert.That(res.StatusCode, Is.EqualTo(502));
            Assert.That(res.Message, Does.Not.Contain("AccessDenied"), "storage internals are not shown to the person");
            Assert.That(_context.HealthArticleImages.Count(), Is.EqualTo(0));
        }

        [Test]
        public async Task WhenTheBucketRefuses_TheCmsTeamGetsTheStorageErrorCode_ButTheContributorDoesNot()
        {
            _store.Setup(s => s.PutAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HealthWikiImageStoreException("403 AccessDenied", new Exception("inner")));
            var me = new HealthWikiContributor { ContributorId = Guid.NewGuid(), Type = "INDEPENDENT_DOCTOR", FullName = "Dr. A", Mobile = "9100000009", Status = "VERIFIED", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            _context.HealthWikiContributors.Add(me); await _context.SaveChangesAsync();

            var cms = await new UploadHealthArticleImageHandler(Service).Handle(new UploadHealthArticleImageRequestModel { File = File(Jpeg), ActorName = "Priya" }, CancellationToken.None);
            var contributor = await new UploadContributorImageHandler(_context, Service).Handle(new UploadContributorImageRequestModel { ContributorId = me.ContributorId, File = File(Jpeg) }, CancellationToken.None);

            Assert.That(cms.Diagnostic, Is.EqualTo("403 AccessDenied"));
            Assert.That(contributor.StatusCode, Is.EqualTo(502));
        }

        [Test]
        public async Task AnAddressThatIsNotHttps_IsRefused_ForTheDatabaseOnlyAcceptsHttps()
        {
            _store.Setup(s => s.PutAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("http://insecure.example.net/articles/a.jpg");

            var res = await Service.UploadAsync(File(Jpeg), null, "CMS", CancellationToken.None);

            Assert.That(res.StatusCode, Is.EqualTo(502));
            Assert.That(_context.HealthArticleImages.Count(), Is.EqualTo(0));
        }

        [Test]
        public async Task OnePersonCannotUploadWithoutLimit()
        {
            var me = Guid.NewGuid();
            for (var i = 0; i < HealthWikiImageRules.MaxUploadsPerDay; i++)
                _context.HealthArticleImages.Add(new HealthArticleImage { ImageId = Guid.NewGuid(), ContributorId = me, Url = "https://x/a.jpg", ContentType = "image/jpeg", SizeBytes = 1, CreatedAt = DateTime.UtcNow.AddHours(-1) });
            _context.HealthArticleImages.Add(new HealthArticleImage { ImageId = Guid.NewGuid(), ContributorId = me, Url = "https://x/old.jpg", ContentType = "image/jpeg", SizeBytes = 1, CreatedAt = DateTime.UtcNow.AddDays(-3) });
            await _context.SaveChangesAsync();

            var blocked = await Service.UploadAsync(File(Jpeg), me, "Dr. Meera", CancellationToken.None);
            var someoneElse = await Service.UploadAsync(File(Jpeg), Guid.NewGuid(), "Dr. Other", CancellationToken.None);

            Assert.That(blocked.StatusCode, Is.EqualTo(429));
            Assert.That(someoneElse.Success, Is.True, "the limit is per person");
        }

        // ------------------------------------------------------------------------------------------ the two doors

        [Test]
        public async Task AContributor_NeedsAnAcceptedProfile()
        {
            var accepted = AddContributor("PENDING"); var invited = AddContributor("INVITED", "9100000002");
            var h = new UploadContributorImageHandler(_context, Service);

            Assert.That((await h.Handle(new UploadContributorImageRequestModel { ContributorId = accepted.ContributorId, File = File(Jpeg) }, CancellationToken.None)).Success, Is.True);
            Assert.That((await h.Handle(new UploadContributorImageRequestModel { ContributorId = invited.ContributorId, File = File(Jpeg) }, CancellationToken.None)).StatusCode, Is.EqualTo(403));
            Assert.That((await h.Handle(new UploadContributorImageRequestModel { ContributorId = Guid.NewGuid(), File = File(Jpeg) }, CancellationToken.None)).StatusCode, Is.EqualTo(404));
            Assert.That(_context.HealthArticleImages.Single().ContributorId, Is.EqualTo(accepted.ContributorId));
        }

        [Test]
        public async Task TheCms_UploadsWithoutAContributor_AndTheHistoryNamesWhoSentIt()
        {
            var res = await new UploadHealthArticleImageHandler(Service).Handle(new UploadHealthArticleImageRequestModel { File = File(Png), ActorName = "Priya" }, CancellationToken.None);

            Assert.That(res.Success, Is.True, res.Message);
            Assert.That(_context.HealthArticleImages.Single().ContributorId, Is.Null);
            Assert.That(_context.HealthArticleImages.Single().UploadedByName, Is.EqualTo("Priya"));
        }

        private HealthWikiContributor AddContributor(string status, string mobile = "9100000001")
        {
            var c = new HealthWikiContributor { ContributorId = Guid.NewGuid(), Type = "INDEPENDENT_DOCTOR", FullName = "Dr. A", Mobile = mobile, Status = status, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            _context.HealthWikiContributors.Add(c);
            _context.SaveChanges();
            return c;
        }
    }
}
