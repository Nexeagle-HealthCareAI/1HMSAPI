using EasyHMSAPI.Application.Services.Models;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>
    /// A CMS editor's decision on an article: APPROVE (sector updates only), WITHDRAW (send back for changes)
    /// UNPUBLISH (take a live article offline) or PULL_BACK (take it offline and put it back in review). A MEDICAL article is approved by its doctor reviewer through the
    /// review endpoints, never here. What is live changes only on APPROVE of a pending edit or on UNPUBLISH.
    /// </summary>
    public class DecideHealthArticleHandler : IRequestHandler<DecideHealthArticleRequestModel, SaveHealthArticleResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IHealthWikiNotifier _notifier;

        public DecideHealthArticleHandler(AppDbContext context, IHealthWikiNotifier notifier)
        {
            _context = context;
            _notifier = notifier;
        }

        public async Task<SaveHealthArticleResponseModel> Handle(DecideHealthArticleRequestModel request, CancellationToken cancellationToken)
        {
            var slug = request.Slug?.Trim().ToLowerInvariant();
            var article = await _context.HealthArticles.FirstOrDefaultAsync(a => a.Slug == slug, cancellationToken);
            if (article == null) return Fail(404, "Article not found.");

            var actor = HealthWikiAuditLog.ActorOrDefault(request.ActorName);
            var reason = request.Reason?.Trim();
            var now = DateTime.UtcNow;
            // The title the message shows: a pending edit may have a newer one than the live article.
            var messageTitle = article.Title;

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
                        article.HoldForReview = false;
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
                        messageTitle = revision.Title;
                        revision.Status = HealthArticleRevision.StatusDraft;
                        revision.ReviewerComment = reason;
                        revision.UpdatedAt = now;
                    }
                    else if (article.Status == HealthArticle.StatusInReview)
                    {
                        article.Status = HealthArticle.StatusDraft;
                        article.ApprovedAt = null; // an approval does not survive being sent back
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

                case DecideHealthArticleRequestModel.PullBack:
                    if (string.IsNullOrEmpty(reason)) return Fail(400, "A reason is required to pull an article back to review.");
                    if (reason.Length > HealthArticleRules.MaxReasonLength) return Fail(400, $"reason is max {HealthArticleRules.MaxReasonLength} chars.");
                    if (article.Status != HealthArticle.StatusPublished) return Fail(409, "The article is not published.");
                    // Offline at once and back in the review queue. The edit in progress (if any) becomes the text under review.
                    article.Status = HealthArticle.StatusInReview;
                    article.PublishedAt = null;
                    article.ApprovedAt = null;
                    article.HoldForReview = true;
                    article.SubmittedAt = now;
                    article.ReviewerComment = reason;
                    article.UpdatedAt = now;
                    if (revision != null)
                    {
                        ArticleContent.From(revision).CopyTo(article);
                        revision.Status = HealthArticleRevision.StatusApplied;
                        revision.ResolvedAt = now;
                        revision.UpdatedAt = now;
                        revision = null;
                    }
                    HealthWikiAuditLog.Add(_context, HealthWikiAudit.EntityArticle, article.ArticleId, "PULLED_BACK", HealthWikiAudit.ActorCmsUser, actor, reason);
                    break;

                default:
                    return Fail(400, "action must be APPROVE, WITHDRAW, UNPUBLISH or PULL_BACK.");
            }

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                return Fail(409, "The article was changed by someone else at the same moment. Reload it and try again.");
            }

            // Saved, so the people are told. Telling them never changes the answer: the notifier does not throw.
            var about = new HealthArticleRef(article.ArticleId, article.Slug, request.Action == DecideHealthArticleRequestModel.Withdraw ? messageTitle : article.Title);
            switch (request.Action)
            {
                case DecideHealthArticleRequestModel.Approve:
                    await _notifier.PublishedAsync(about, article.AuthorContributorId, null, cancellationToken);
                    break;
                case DecideHealthArticleRequestModel.Withdraw:
                    await _notifier.ChangesRequestedAsync(about, article.AuthorContributorId, "the NexEagle editorial team", reason!, byEditor: true, cancellationToken);
                    break;
                case DecideHealthArticleRequestModel.Unpublish:
                    await _notifier.ArticleRemovedAsync(about, article.AuthorContributorId, article.ReviewerContributorId, reason!, cancellationToken);
                    break;
                case DecideHealthArticleRequestModel.PullBack:
                    await _notifier.ArticleRemovedAsync(about, article.AuthorContributorId, article.ReviewerContributorId, "Pulled back for review: " + reason, cancellationToken);
                    break;
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
