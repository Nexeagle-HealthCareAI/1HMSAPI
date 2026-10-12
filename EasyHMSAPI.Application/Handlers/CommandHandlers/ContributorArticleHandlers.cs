using EasyHMSAPI.Application.Services.Models;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    internal static class ContributorArticles
    {
        public static async Task<MyArticleInfo?> Reload(AppDbContext context, Guid me, string slug, CancellationToken ct) =>
            (await ContributorArticleLoader.Load(context, me, slug, ct)).FirstOrDefault();

        public static async Task<HealthArticleRevision?> OpenRevision(AppDbContext context, Guid articleId, CancellationToken ct) =>
            await context.HealthArticleRevisions.FirstOrDefaultAsync(
                r => r.ArticleId == articleId && (r.Status == HealthArticleRevision.StatusDraft || r.Status == HealthArticleRevision.StatusInReview), ct);
    }

    /// <summary>
    /// Create or edit the contributor's own article. The rules (who may author what, https cover with alt text, a published
    /// article's edit held as a pending revision) are those of the CMS save; this adds ownership and "no edits while with the reviewer".
    /// </summary>
    public class SaveContributorArticleHandler : IRequestHandler<SaveContributorArticleRequestModel, ContributorArticleResponseModel>
    {
        private static readonly HashSet<string> AllKeys = new(StringComparer.Ordinal)
        {
            "type", "title", "description", "content", "coverImageUrl", "coverImageAlt", "relatedConditionSlug", "disclosure", "references",
        };

        private readonly AppDbContext _context;
        private readonly IHealthArticleService _articles;

        public SaveContributorArticleHandler(AppDbContext context, IHealthArticleService articles)
        {
            _context = context;
            _articles = articles;
        }

        public async Task<ContributorArticleResponseModel> Handle(SaveContributorArticleRequestModel r, CancellationToken ct)
        {
            var me = await _context.HealthWikiContributors.AsNoTracking().FirstOrDefaultAsync(c => c.ContributorId == r.ContributorId, ct);
            if (me == null) return ContributorArticleResponseModel.Fail(404, "Profile not found.");
            if (!ContributorPortalRules.IsProfileAccepted(me.Status))
                return ContributorArticleResponseModel.Fail(403, "Your profile has not been accepted yet. Finish registering first.");

            var type = HealthArticleRules.NormalizeType(r.Type);
            if (r.Type != null && type == null) return ContributorArticleResponseModel.Fail(400, "type must be MEDICAL or SECTOR_UPDATE.");

            string slug;
            var isCreate = string.IsNullOrWhiteSpace(r.Slug);
            if (isCreate)
            {
                type ??= HealthArticle.TypeMedical;
                if (!HealthArticleRules.CanAuthor(type, me.Type))
                    return ContributorArticleResponseModel.Fail(403, "Only doctors can write medical articles. You can write sector updates.");
                if (string.IsNullOrWhiteSpace(r.Title)) return ContributorArticleResponseModel.Fail(400, "Title is required.");
                slug = await UniqueSlug(ContributorPortalRules.Slugify(r.Title), ct);
            }
            else
            {
                slug = r.Slug!.Trim().ToLowerInvariant();
                var article = await _context.HealthArticles.AsNoTracking().FirstOrDefaultAsync(a => a.Slug == slug && a.AuthorContributorId == me.ContributorId, ct);
                if (article == null) return ContributorArticleResponseModel.Fail(404, "Article not found.");
                var revision = await ContributorArticles.OpenRevision(_context, article.ArticleId, ct);
                if (article.Status == HealthArticle.StatusInReview || revision?.Status == HealthArticleRevision.StatusInReview)
                    return ContributorArticleResponseModel.Fail(409, "This article is with the reviewer. Wait for their decision before editing.");
                if (type != null && type != article.Type && !HealthArticleRules.CanAuthor(type, me.Type))
                    return ContributorArticleResponseModel.Fail(403, "Only doctors can write medical articles. You can write sector updates.");
            }

            var provided = new HashSet<string>(AllKeys, StringComparer.Ordinal);
            if (type == null) provided.Remove("type");
            if (isCreate) provided.Add("authorContributorId");

            var result = await _articles.SaveAsync(new SaveHealthArticleRequestModel
            {
                IsCreate = isCreate,
                Slug = slug,
                Type = type,
                Title = r.Title,
                Description = r.Description,
                Content = r.Content,
                CoverImageUrl = r.CoverImageUrl,
                CoverImageAlt = r.CoverImageAlt,
                RelatedConditionSlug = r.RelatedConditionSlug,
                Disclosure = r.Disclosure,
                References = r.References,
                AuthorContributorId = isCreate ? me.ContributorId : null,
                ActorName = me.FullName,
                ActorType = HealthWikiAudit.ActorContributor,
                AllowEmptyContent = true,
                Provided = provided,
            }, ct);
            if (!result.Success) return ContributorArticleResponseModel.Fail(result.StatusCode, result.Message ?? "The article could not be saved.");

            if (!isCreate)
            {
                // The author has started writing the article the team commissioned from their topic.
                var articleId = await _context.HealthArticles.Where(a => a.Slug == slug).Select(a => a.ArticleId).FirstAsync(ct);
                var topic = await _context.HealthWikiTopicRequests.FirstOrDefaultAsync(t => t.ArticleId == articleId && t.Status == HealthWikiTopicRequest.StatusAccepted, ct);
                if (topic != null)
                {
                    topic.Status = HealthWikiTopicRequest.StatusArticleStarted;
                    topic.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync(ct);
                }
            }

            var mine = await ContributorArticles.Reload(_context, me.ContributorId, slug, ct);
            return mine == null ? ContributorArticleResponseModel.Fail(500, "The article was saved but could not be read back.") : ContributorArticleResponseModel.Ok(mine, isCreate ? 201 : 200);
        }

        private async Task<string> UniqueSlug(string baseSlug, CancellationToken ct)
        {
            var slug = baseSlug;
            for (var i = 2; await _context.HealthArticles.AnyAsync(a => a.Slug == slug, ct); i++)
            {
                var suffix = "-" + i;
                slug = (baseSlug.Length + suffix.Length > HealthArticleRules.MaxSlugLength ? baseSlug[..(HealthArticleRules.MaxSlugLength - suffix.Length)] : baseSlug) + suffix;
            }
            return slug;
        }
    }

    /// <summary>
    /// The author sends a draft for review (or, for a live article, the edit waiting). Needs a VERIFIED profile: the team
    /// has checked who is writing. A medical article may be sent before a doctor is assigned; the CMS assigns one next.
    /// </summary>
    public class SubmitContributorArticleHandler : IRequestHandler<SubmitContributorArticleRequestModel, ContributorArticleResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IHealthWikiNotifier _notifier;

        public SubmitContributorArticleHandler(AppDbContext context, IHealthWikiNotifier notifier)
        {
            _context = context;
            _notifier = notifier;
        }

        public async Task<ContributorArticleResponseModel> Handle(SubmitContributorArticleRequestModel r, CancellationToken ct)
        {
            var me = await _context.HealthWikiContributors.AsNoTracking().FirstOrDefaultAsync(c => c.ContributorId == r.ContributorId, ct);
            if (me == null) return ContributorArticleResponseModel.Fail(404, "Profile not found.");
            // A filled-in profile the team has not rejected can send an article for review; it is the reviewer (or the CMS editor for a
            // sector update) who decides whether it goes live, so the author's own profile check is not a gate here.
            if (!ContributorPortalRules.IsProfileAccepted(me.Status))
                return ContributorArticleResponseModel.Fail(403, me.Status == HealthWikiContributor.StatusRejected
                    ? "This account cannot send articles."
                    : "Finish your profile before you send an article for review.");
            if (!r.SubmitConsent)
                return ContributorArticleResponseModel.Fail(400, "Please confirm the consent to send the article for review.");

            var slug = r.Slug?.Trim().ToLowerInvariant();
            var article = await _context.HealthArticles.FirstOrDefaultAsync(a => a.Slug == slug && a.AuthorContributorId == me.ContributorId, ct);
            if (article == null) return ContributorArticleResponseModel.Fail(404, "Article not found.");
            var revision = await ContributorArticles.OpenRevision(_context, article.ArticleId, ct);
            var now = DateTime.UtcNow;

            if (article.Status == HealthArticle.StatusDraft)
            {
                var problem = HealthArticleRules.ValidateContent(ArticleContent.From(article));
                if (problem != null) return ContributorArticleResponseModel.Fail(400, FriendlyProblem(problem));
                article.Status = HealthArticle.StatusInReview;
                article.SubmittedAt = now;
                article.ReviewerComment = null;
                article.ApprovedAt = null;
                article.UpdatedAt = now;
            }
            else if (article.Status == HealthArticle.StatusPublished && revision?.Status == HealthArticleRevision.StatusDraft)
            {
                var problem = HealthArticleRules.ValidateContent(ArticleContent.From(revision));
                if (problem != null) return ContributorArticleResponseModel.Fail(400, FriendlyProblem(problem));
                revision.Status = HealthArticleRevision.StatusInReview;
                revision.ReviewerComment = null;
                revision.UpdatedAt = now;
                article.SubmittedAt = now;
            }
            else
            {
                return ContributorArticleResponseModel.Fail(409, "Only a draft can be submitted.");
            }

            HealthWikiAuditLog.Add(_context, HealthWikiAudit.EntityArticle, article.ArticleId, "SUBMITTED", HealthWikiAudit.ActorContributor, me.FullName,
                (revision != null && article.Status == HealthArticle.StatusPublished ? "Edit of the published article. " : "") + "Author agreed the reviewer may publish it if it is right.");
            try
            {
                await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                return ContributorArticleResponseModel.Fail(409, "The article was changed at the same moment. Reload it and try again.");
            }

            // A medical article with a doctor assigned goes to that doctor now (N5). Without one it waits, and the doctor is told when the team assigns them.
            if (article.Type == HealthArticle.TypeMedical && article.ReviewerContributorId.HasValue)
                await _notifier.ReviewRequestedAsync(new HealthArticleRef(article.ArticleId, article.Slug,
                    article.Status == HealthArticle.StatusPublished && revision != null ? revision.Title : article.Title), article.ReviewerContributorId.Value, ct);

            var mine = await ContributorArticles.Reload(_context, me.ContributorId, article.Slug, ct);
            return ContributorArticleResponseModel.Ok(mine!);
        }

        private static string FriendlyProblem(string problem) =>
            problem.StartsWith("title") || problem.StartsWith("content") ? "Add a title and some content first." : problem;
    }

    /// <summary>
    /// The assigned doctor's decision from the Doctor Dekho pages. The rules live in IHealthArticleReviewService (shared with the
    /// EasyHMS staff review); this returns the article as the doctor sees it afterwards.
    /// </summary>
    public class ReviewContributorArticleHandler : IRequestHandler<ReviewContributorArticleRequestModel, ContributorArticleResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IHealthArticleReviewService _reviews;

        public ReviewContributorArticleHandler(AppDbContext context, IHealthArticleReviewService reviews)
        {
            _context = context;
            _reviews = reviews;
        }

        public async Task<ContributorArticleResponseModel> Handle(ReviewContributorArticleRequestModel r, CancellationToken ct)
        {
            var result = await _reviews.ReviewAsync(r.ContributorId, r.Slug, r.Decision, r.Comment, r.AccuracyConfirmed, ct);
            if (!result.Success) return ContributorArticleResponseModel.Fail(result.StatusCode, result.Message!);

            var mine = await ContributorArticles.Reload(_context, r.ContributorId, result.Article!.Slug, ct);
            // After "request changes" the article is back with its author and no longer visible to the reviewer; say so plainly.
            return mine != null ? ContributorArticleResponseModel.Ok(mine)
                : ContributorArticleResponseModel.Ok(ContributorPortalMapper.ToMyArticle(result.Article, null, r.ContributorId, null, result.ReviewerName, DateTime.UtcNow));
        }
    }
}
