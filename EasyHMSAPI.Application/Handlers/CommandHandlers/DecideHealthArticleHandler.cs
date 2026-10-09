using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>
    /// A CMS editor's decision on an article: APPROVE (sector updates only), WITHDRAW (send back for changes)
    /// or UNPUBLISH (take a live article offline). A MEDICAL article is approved by its doctor reviewer through the
    /// review endpoints, never here. What is live changes only on APPROVE of a pending edit or on UNPUBLISH.
    /// </summary>
    public class DecideHealthArticleHandler : IRequestHandler<DecideHealthArticleRequestModel, SaveHealthArticleResponseModel>
    {
        private readonly AppDbContext _context;

        public DecideHealthArticleHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<SaveHealthArticleResponseModel> Handle(DecideHealthArticleRequestModel request, CancellationToken cancellationToken)
        {
            var slug = request.Slug?.Trim().ToLowerInvariant();
            var article = await _context.HealthArticles.FirstOrDefaultAsync(a => a.Slug == slug, cancellationToken);
            if (article == null) return Fail(404, "Article not found.");

            var actor = HealthWikiAuditLog.ActorOrDefault(request.ActorName);
            var reason = request.Reason?.Trim();
            var now = DateTime.UtcNow;

            var revision = await _context.HealthArticleRevisions.FirstOrDefaultAsync(
                r => r.ArticleId == article.ArticleId && (r.Status == HealthArticleRevision.StatusDraft || r.Status == HealthArticleRevision.StatusInReview),
                cancellationToken);

            switch (request.Action)
            {
                case DecideHealthArticleRequestModel.Approve:
                    if (article.Type == HealthArticle.TypeMedical)
                        return Fail(400, "A medical article is approved by its doctor reviewer, not by the CMS.");
                    if (revision != null && article.Status == HealthArticle.StatusPublished)
                    {
                        var content = ArticleContent.From(revision);
                        var problem = HealthArticleRules.ValidateContent(content);
                        if (problem != null) return Fail(400, problem);
                        content.CopyTo(article);
                        revision.Status = HealthArticleRevision.StatusApplied;
                        revision.ResolvedAt = now;
                        revision.UpdatedAt = now;
                        article.ApprovedAt = now;
                        article.ApprovedByName = actor;
                        article.UpdatedAt = now;
                        HealthWikiAuditLog.Add(_context, HealthWikiAudit.EntityArticle, article.ArticleId, "REVISION_APPLIED", HealthWikiAudit.ActorCmsUser, actor);
                        revision = null;
                    }
                    else if (article.Status == HealthArticle.StatusPublished)
                    {
                        return Fail(409, "The article is already published and has no edit waiting for approval.");
                    }
                    else
                    {
                        var problem = HealthArticleRules.ValidateContent(ArticleContent.From(article));
                        if (problem != null) return Fail(400, problem);
                        article.Status = HealthArticle.StatusPublished;
                        article.PublishedAt ??= now;
                        article.ApprovedAt = now;
                        article.ApprovedByName = actor;
                        article.ReviewerComment = null;
                        article.UpdatedAt = now;
                        HealthWikiAuditLog.Add(_context, HealthWikiAudit.EntityArticle, article.ArticleId, "APPROVED", HealthWikiAudit.ActorCmsUser, actor);
                    }
                    break;

                case DecideHealthArticleRequestModel.Withdraw:
                    if (string.IsNullOrEmpty(reason)) return Fail(400, "A reason is required so the author knows what to change.");
                    if (reason.Length > HealthArticleRules.MaxReasonLength) return Fail(400, $"reason is max {HealthArticleRules.MaxReasonLength} chars.");
                    if (revision != null && revision.Status == HealthArticleRevision.StatusInReview)
                    {
                        // Only the pending edit goes back; the live article is not touched.
                        revision.Status = HealthArticleRevision.StatusDraft;
                        revision.ReviewerComment = reason;
                        revision.UpdatedAt = now;
                    }
                    else if (article.Status == HealthArticle.StatusInReview)
                    {
                        article.Status = HealthArticle.StatusDraft;
                        article.ReviewerComment = reason;
                        article.UpdatedAt = now;
                    }
                    else
                    {
                        return Fail(409, article.Status == HealthArticle.StatusPublished
                            ? "Nothing is waiting for review on this live article. Use unpublish to take it offline."
                            : "The article is not in review.");
                    }
                    HealthWikiAuditLog.Add(_context, HealthWikiAudit.EntityArticle, article.ArticleId, "SENT_BACK", HealthWikiAudit.ActorCmsUser, actor, reason);
                    break;

                case DecideHealthArticleRequestModel.Unpublish:
                    if (string.IsNullOrEmpty(reason)) return Fail(400, "A reason is required to take an article offline.");
                    if (reason.Length > HealthArticleRules.MaxReasonLength) return Fail(400, $"reason is max {HealthArticleRules.MaxReasonLength} chars.");
                    if (article.Status != HealthArticle.StatusPublished) return Fail(409, "The article is not published.");
                    article.Status = HealthArticle.StatusDraft;
                    article.PublishedAt = null;
                    article.ApprovedAt = null; // a medical article needs its reviewer's approval again before it can go live
                    article.ReviewerComment = reason;
                    article.UpdatedAt = now;
                    if (revision != null)
                    {
                        // The article is offline, so the edit in progress becomes its draft text.
                        ArticleContent.From(revision).CopyTo(article);
                        revision.Status = HealthArticleRevision.StatusApplied;
                        revision.ResolvedAt = now;
                        revision.UpdatedAt = now;
                        revision = null;
                    }
                    HealthWikiAuditLog.Add(_context, HealthWikiAudit.EntityArticle, article.ArticleId, "UNPUBLISHED", HealthWikiAudit.ActorCmsUser, actor, reason);
                    break;

                default:
                    return Fail(400, "action must be APPROVE, WITHDRAW or UNPUBLISH.");
            }

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                return Fail(409, "The article was changed by someone else at the same moment. Reload it and try again.");
            }

            return new SaveHealthArticleResponseModel
            {
                Success = true,
                StatusCode = 200,
                Slug = article.Slug,
                Status = article.Status,
                PublishedAt = article.PublishedAt,
                Article = HealthArticleAdminMapper.ToInfo(article, revision),
            };
        }

        private static SaveHealthArticleResponseModel Fail(int statusCode, string message) =>
            new() { Success = false, StatusCode = statusCode, Message = message };
    }
}
