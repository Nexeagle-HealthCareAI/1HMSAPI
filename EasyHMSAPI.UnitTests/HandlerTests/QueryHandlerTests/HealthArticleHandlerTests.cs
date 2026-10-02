using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class HealthArticleHandlerTests
    {
        private AppDbContext _context = null!;
        private SaveHealthArticleHandler _save = null!;
        private GetPublicHealthArticlesHandler _get = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _save = new SaveHealthArticleHandler(_context);
            _get = new GetPublicHealthArticlesHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private static SaveHealthArticleRequestModel NewArticle(string slug = "diabetes-type-2", string? status = null) => new()
        {
            IsCreate = true, Slug = slug, Title = "Understanding Type 2 Diabetes",
            Description = "Guide", Content = "## What is it?", Status = status,
        };

        [TestCase("diabetes-type-2", true)]
        [TestCase("a", true)]
        [TestCase("Diabetes", false)]
        [TestCase("two--hyphens", false)]
        [TestCase("-leading", false)]
        [TestCase("has space", false)]
        [TestCase("", false)]
        public void IsValidSlug_ChecksFormat(string slug, bool expected) =>
            Assert.That(HealthArticleRules.IsValidSlug(slug), Is.EqualTo(expected));

        [TestCase("published", "PUBLISHED")]
        [TestCase("in-review", "IN_REVIEW")]
        [TestCase("In Review", "IN_REVIEW")]
        [TestCase("draft", "DRAFT")]
        [TestCase("archived", null)]
        public void NormalizeStatus_MapsKnownValues(string input, string? expected) =>
            Assert.That(HealthArticleRules.NormalizeStatus(input), Is.EqualTo(expected));

        [Test]
        public async Task Create_DefaultsToDraft_AndPublicApiHidesIt()
        {
            var res = await _save.Handle(NewArticle(), CancellationToken.None);

            Assert.That(res.Success, Is.True);
            Assert.That(res.StatusCode, Is.EqualTo(201));
            Assert.That(res.Status, Is.EqualTo("DRAFT"));
            Assert.That(res.PublishedAt, Is.Null);
            var list = await _get.Handle(new GetPublicHealthArticlesRequestModel(), CancellationToken.None);
            Assert.That(list.Articles, Is.Empty);
        }

        [Test]
        public async Task Create_Published_IsReturnedWithSlugAsId()
        {
            await _save.Handle(NewArticle(status: "PUBLISHED"), CancellationToken.None);

            var list = await _get.Handle(new GetPublicHealthArticlesRequestModel(), CancellationToken.None);
            var single = await _get.Handle(new GetPublicHealthArticlesRequestModel { Slug = "Diabetes-Type-2", PageSize = 1 }, CancellationToken.None);

            Assert.That(list.Articles, Has.Count.EqualTo(1));
            Assert.That(list.Articles[0].Id, Is.EqualTo("diabetes-type-2"));
            Assert.That(list.Articles[0].PublishedAt, Is.Not.Null);
            Assert.That(single.Articles, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task Create_DuplicateSlug_Returns409()
        {
            await _save.Handle(NewArticle(), CancellationToken.None);
            var res = await _save.Handle(NewArticle(), CancellationToken.None);

            Assert.That(res.Success, Is.False);
            Assert.That(res.StatusCode, Is.EqualTo(409));
        }

        [Test]
        public async Task Create_InvalidSlugStatusOrMissingFields_Returns400()
        {
            var badSlug = await _save.Handle(NewArticle("Bad Slug"), CancellationToken.None);
            var badStatus = await _save.Handle(NewArticle(status: "archived"), CancellationToken.None);
            var noContent = await _save.Handle(new SaveHealthArticleRequestModel { IsCreate = true, Slug = "x", Title = "t" }, CancellationToken.None);

            Assert.That(new[] { badSlug.StatusCode, badStatus.StatusCode, noContent.StatusCode }, Is.All.EqualTo(400));
        }

        [Test]
        public async Task Create_UnknownDoctor_Returns400()
        {
            var req = NewArticle();
            req.ReviewerDoctorId = Guid.NewGuid();

            var res = await _save.Handle(req, CancellationToken.None);

            Assert.That(res.StatusCode, Is.EqualTo(400));
        }

        [Test]
        public async Task Update_PublishThenRevert_StampsAndClearsPublishedAt_AndKeepsUntouchedFields()
        {
            await _save.Handle(NewArticle(), CancellationToken.None);

            var published = await _save.Handle(new SaveHealthArticleRequestModel { Slug = "diabetes-type-2", Status = "PUBLISHED" }, CancellationToken.None);
            var firstStamp = published.PublishedAt;
            var edited = await _save.Handle(new SaveHealthArticleRequestModel { Slug = "diabetes-type-2", Title = "New title" }, CancellationToken.None);
            var article = _context.HealthArticles.Single();

            Assert.That(firstStamp, Is.Not.Null);
            Assert.That(edited.PublishedAt, Is.EqualTo(firstStamp), "editing a published article keeps its original PublishedAt");
            Assert.That(article.Title, Is.EqualTo("New title"));
            Assert.That(article.Content, Is.EqualTo("## What is it?"));

            var reverted = await _save.Handle(new SaveHealthArticleRequestModel { Slug = "diabetes-type-2", Status = "IN_REVIEW" }, CancellationToken.None);
            Assert.That(reverted.PublishedAt, Is.Null);
            var list = await _get.Handle(new GetPublicHealthArticlesRequestModel(), CancellationToken.None);
            Assert.That(list.Articles, Is.Empty);
        }

        [Test]
        public async Task Update_UnknownSlug_Returns404()
        {
            var res = await _save.Handle(new SaveHealthArticleRequestModel { Slug = "nope", Title = "x" }, CancellationToken.None);

            Assert.That(res.StatusCode, Is.EqualTo(404));
        }

        [Test]
        public async Task Create_WithExistingDoctors_Succeeds()
        {
            var doctorId = Guid.NewGuid();
            _context.Doctors.Add(new Doctor { DoctorID = doctorId, UserID = Guid.NewGuid(), LicenseNumber = "BMC-1" });
            await _context.SaveChangesAsync();
            var req = NewArticle(status: "PUBLISHED");
            req.AuthorDoctorId = doctorId;
            req.ReviewerDoctorId = doctorId;

            var res = await _save.Handle(req, CancellationToken.None);
            var list = await _get.Handle(new GetPublicHealthArticlesRequestModel(), CancellationToken.None);

            Assert.That(res.Success, Is.True);
            Assert.That(list.Articles[0].ReviewerDoctorId, Is.EqualTo(doctorId));
        }
    }
}
