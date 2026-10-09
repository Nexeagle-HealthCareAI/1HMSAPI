using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services;
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
    public class SaveContributorArticleHandler : IRequestHandler<SaveContributorArticleRequestModel, ApiResult<MyArticleInfo>>
    {
        private static readonly HashSet<string> AllKeys = new(StringComparer.Ordinal)
        {
            "type", "title", "description", "content", "coverImageUrl", "coverImageAlt", "relatedConditionSlug", "disclosure", "references",
        };

        private readonly AppDbContext _context;

        public SaveContributorArticleHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ApiResult<MyArticleInfo>> Handle(SaveContributorArticleRequestModel r, CancellationToken ct)
        {
            var me = await _context.HealthWikiContributors.AsNoTracking().FirstOrDefaultAsync(c => c.ContributorId == r.ContributorId, ct);
            if (me == null) return ApiResult<MyArticleInfo>.Fail(404, "Profile not found.");
            if (!ContributorPortalRules.IsProfileAccepted(me.Status))
                return ApiResult<MyArticleInfo>.Fail(403, "Your profile has not been accepted yet. Finish registering first.");

            var type = HealthArticleRules.NormalizeType(r.Type);
            if (r.Type != null && type == null) return ApiResult<MyArticleInfo>.Fail(400, "type must be MEDICAL or SECTOR_UPDATE.");

            string slug;
            var isCreate = string.IsNullOrWhiteSpace(r.Slug);
            if (isCreate)
            {
                type ??= HealthArticle.TypeMedical;
                if (!HealthArticleRules.CanAuthor(type, me.Type))
                    return ApiResult<MyArticleInfo>.Fail(403, "Only doctors can write medical articles. You can write sector updates.");
                if (string.IsNullOrWhiteSpace(r.Title)) return ApiResult<MyArticleInfo>.Fail(400, "Title is required.");
                slug = await UniqueSlug(ContributorPortalRules.Slugify(r.Title), ct);
            }
            else
            {
                slug = r.Slug!.Trim().ToLowerInvariant();
                var article = await _context.HealthArticles.AsNoTracking().FirstOrDefaultAsync(a => a.Slug == slug && a.AuthorContributorId == me.ContributorId, ct);
                if (article == null) return ApiResult<MyArticleInfo>.Fail(404, "Article not found.");
                var revision = await ContributorArticles.OpenRevision(_context, article.ArticleId, ct);
                if (article.Status == HealthArticle.StatusInReview || revision?.Status == HealthArticleRevision.StatusInReview)
                    return ApiResult<MyArticleInfo>.Fail(409, "This article is with the reviewer. Wait for their decision before editing.");
                if (type != null && type != article.Type && !HealthArticleRules.CanAuthor(type, me.Type))
                    return ApiResult<MyArticleInfo>.Fail(403, "Only doctors can write medical articles. You can write sector updates.");
            }

            var provided = new HashSet<string>(AllKeys, StringComparer.Ordinal);
            if (type == null) provided.Remove("type");
            if (isCreate) provided.Add("authorContributorId");

            var result = await new SaveHealthArticleHandler(_context).Handle(new SaveHealthArticleRequestModel
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
            if (!result.Success) return ApiResult<MyArticleInfo>.Fail(result.StatusCode, result.Message ?? "The article could not be saved.");

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
            return mine == null ? ApiResult<MyArticleInfo>.Fail(500, "The article was saved but could not be read back.") : ApiResult<MyArticleInfo>.Ok(mine, isCreate ? 201 : 200);
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
    public class SubmitContributorArticleHandler : IRequestHandler<SubmitContributorArticleRequestModel, ApiResult<MyArticleInfo>>
    {
        private readonly AppDbContext _context;

        public SubmitContributorArticleHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ApiResult<MyArticleInfo>> Handle(SubmitContributorArticleRequestModel r, CancellationToken ct)
        {
            var me = await _context.HealthWikiContributors.AsNoTracking().FirstOrDefaultAsync(c => c.ContributorId == r.ContributorId, ct);
            if (me == null) return ApiResult<MyArticleInfo>.Fail(404, "Profile not found.");
            if (me.Status != HealthWikiContributor.StatusVerified)
                return ApiResult<MyArticleInfo>.Fail(403, "You can submit articles after the team has checked your profile.");

            var slug = r.Slug?.Trim().ToLowerInvariant();
            var article = await _context.HealthArticles.FirstOrDefaultAsync(a => a.Slug == slug && a.AuthorContributorId == me.ContributorId, ct);
            if (article == null) return ApiResult<MyArticleInfo>.Fail(404, "Article not found.");
            var revision = await ContributorArticles.OpenRevision(_context, article.ArticleId, ct);
            var now = DateTime.UtcNow;

            if (article.Status == HealthArticle.StatusDraft)
            {
                var problem = HealthArticleRules.ValidateContent(ArticleContent.From(article));
                if (problem != null) return ApiResult<MyArticleInfo>.Fail(400, FriendlyProblem(problem));
                article.Status = HealthArticle.StatusInReview;
                article.SubmittedAt = now;
                article.ReviewerComment = null;
                article.ApprovedAt = null;
                article.UpdatedAt = now;
            }
            else if (article.Status == HealthArticle.StatusPublished && revision?.Status == HealthArticleRevision.StatusDraft)
            {
                var problem = HealthArticleRules.ValidateContent(ArticleContent.From(revision));
                if (problem != null) return ApiResult<MyArticleInfo>.Fail(400, FriendlyProblem(problem));
                revision.Status = HealthArticleRevision.StatusInReview;
                revision.ReviewerComment = null;
                revision.UpdatedAt = now;
                article.SubmittedAt = now;
            }
            else
            {
                return ApiResult<MyArticleInfo>.Fail(409, "Only a draft can be submitted.");
            }

            HealthWikiAuditLog.Add(_context, HealthWikiAudit.EntityArticle, article.ArticleId, "SUBMITTED", HealthWikiAudit.ActorContributor, me.FullName,
                revision != null && article.Status == HealthArticle.StatusPublished ? "Edit of the published article" : null);
            try
            {
                await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                return ApiResult<MyArticleInfo>.Fail(409, "The article was changed at the same moment. Reload it and try again.");
            }

            var mine = await ContributorArticles.Reload(_context, me.ContributorId, article.Slug, ct);
            return ApiResult<MyArticleInfo>.Ok(mine!);
        }

        private static string FriendlyProblem(string problem) =>
            problem.StartsWith("title") || problem.StartsWith("content") ? "Add a title and some content first." : problem;
    }

    /// <summary>
    /// The assigned doctor's decision. Only the reviewer named on the article can decide, and only once it is with them.
    /// APPROVE needs the accuracy confirmation; the article goes live at once if the doctor is VERIFIED, otherwise it waits
    /// and goes live when the team verifies them. An approval of an EDIT to a live article needs a VERIFIED doctor.
    /// REQUEST_CHANGES needs a comment of at least 10 characters and sends the article (or edit) back to the author.
    /// Every decision is kept in HealthArticleReview with the name and registration as they were that day.
    /// </summary>
    public class ReviewContributorArticleHandler : IRequestHandler<ReviewContributorArticleRequestModel, ApiResult<MyArticleInfo>>
    {
        private readonly AppDbContext _context;

        public ReviewContributorArticleHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ApiResult<MyArticleInfo>> Handle(ReviewContributorArticleRequestModel r, CancellationToken ct)
        {
            var me = await _context.HealthWikiContributors.FirstOrDefaultAsync(c => c.ContributorId == r.ContributorId, ct);
            if (me == null) return ApiResult<MyArticleInfo>.Fail(404, "Profile not found.");

            var slug = r.Slug?.Trim().ToLowerInvariant();
            var article = await _context.HealthArticles.FirstOrDefaultAsync(a => a.Slug == slug && a.ReviewerContributorId == me.ContributorId, ct);
            if (article == null) return ApiResult<MyArticleInfo>.Fail(404, "Article not found.");
            if (!HealthArticleRules.IsDoctorType(me.Type)) return ApiResult<MyArticleInfo>.Fail(403, "Only doctors can review.");

            var decision = r.Decision?.Trim().ToUpperInvariant();
            if (decision != ReviewContributorArticleRequestModel.Approve && decision != ReviewContributorArticleRequestModel.RequestChanges)
                return ApiResult<MyArticleInfo>.Fail(400, "decision must be APPROVE or REQUEST_CHANGES.");

            var revision = await ContributorArticles.OpenRevision(_context, article.ArticleId, ct);
            var reviewingEdit = article.Status == HealthArticle.StatusPublished && revision?.Status == HealthArticleRevision.StatusInReview;
            var reviewingArticle = article.Status == HealthArticle.StatusInReview && article.ApprovedAt == null;
            if (!reviewingEdit && !reviewingArticle)
                return ApiResult<MyArticleInfo>.Fail(409, "This article is not waiting for your review.");

            var now = DateTime.UtcNow;
            string? comment = null;

            if (decision == ReviewContributorArticleRequestModel.RequestChanges)
            {
                comment = r.Comment?.Trim();
                if (comment == null || comment.Length < HealthArticleRules.MinChangeCommentLength)
                    return ApiResult<MyArticleInfo>.Fail(400, "Add a short comment so the writer knows what to change.");
                if (comment.Length > HealthArticleRules.MaxReasonLength)
                    return ApiResult<MyArticleInfo>.Fail(400, $"The comment is too long (max {HealthArticleRules.MaxReasonLength} characters).");

                if (reviewingEdit)
                {
                    revision!.Status = HealthArticleRevision.StatusDraft;
                    revision.ReviewerComment = comment;
                    revision.UpdatedAt = now;
                }
                else
                {
                    article.Status = HealthArticle.StatusDraft;
                    article.ReviewerComment = comment;
                    article.ApprovedAt = null;
                    article.UpdatedAt = now;
                }
            }
            else
            {
                if (!r.AccuracyConfirmed)
                    return ApiResult<MyArticleInfo>.Fail(400, "Confirm that you have checked the article for medical accuracy.");

                if (reviewingEdit)
                {
                    if (me.Status != HealthWikiContributor.StatusVerified)
                        return ApiResult<MyArticleInfo>.Fail(403, "You can approve edits to a live article after the team has verified your registration.");
                    HealthArticlePublisher.ApplyRevision(_context, article, revision!, HealthWikiAudit.ActorContributor, me.FullName, now);
                    article.ApprovedAt = now;
                }
                else
                {
                    article.ApprovedAt = now;
                    article.ApprovedByName = me.FullName;
                    // Goes live now when the doctor is verified; otherwise it waits and the verification publishes it.
                    HealthArticlePublisher.TryPublish(_context, article, me, me.FullName, now);
                    article.UpdatedAt = now;
                }
            }

            _context.HealthArticleReviews.Add(new HealthArticleReview
            {
                ReviewId = Guid.NewGuid(),
                ArticleId = article.ArticleId,
                ReviewerContributorId = me.ContributorId,
                Decision = decision == ReviewContributorArticleRequestModel.Approve ? "APPROVE" : "REQUEST_CHANGES",
                Comment = comment,
                AccuracyConfirmed = decision == ReviewContributorArticleRequestModel.Approve,
                ReviewerNameSnapshot = me.FullName,
                RegistrationSnapshot = ContributorPortalRules.RegistrationLine(me),
                DecidedAt = now,
            });
            HealthWikiAuditLog.Add(_context, HealthWikiAudit.EntityArticle, article.ArticleId,
                decision == ReviewContributorArticleRequestModel.Approve ? "REVIEWER_APPROVED" : "REVIEWER_REQUESTED_CHANGES",
                HealthWikiAudit.ActorContributor, me.FullName, comment);

            try
            {
                await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                return ApiResult<MyArticleInfo>.Fail(409, "The article was changed at the same moment. Reload it and try again.");
            }

            var mine = await ContributorArticles.Reload(_context, me.ContributorId, article.Slug, ct);
            // After "request changes" the article is back with its author and no longer visible to the reviewer; say so plainly.
            return mine != null ? ApiResult<MyArticleInfo>.Ok(mine)
                : ApiResult<MyArticleInfo>.Ok(ContributorPortalMapper.ToMyArticle(article, null, me.ContributorId, null, me.FullName, now));
        }
    }
}
