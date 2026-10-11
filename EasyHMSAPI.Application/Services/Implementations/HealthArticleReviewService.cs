using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Application.Services.Models;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Services.Implementations
{
    public class HealthArticleReviewService : IHealthArticleReviewService
    {
        private readonly AppDbContext _context;

        public HealthArticleReviewService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<HealthArticleReviewResult> ReviewAsync(Guid reviewerContributorId, string? slugInput, string? decisionInput, string? commentInput, bool accuracyConfirmed, CancellationToken ct)
        {
            var me = await _context.HealthWikiContributors.FirstOrDefaultAsync(c => c.ContributorId == reviewerContributorId, ct);
            if (me == null) return HealthArticleReviewResult.Fail(404, "Profile not found.");

            var slug = slugInput?.Trim().ToLowerInvariant();
            var article = await _context.HealthArticles.FirstOrDefaultAsync(a => a.Slug == slug && a.ReviewerContributorId == me.ContributorId, ct);
            if (article == null) return HealthArticleReviewResult.Fail(404, "Article not found.");
            if (!HealthArticleRules.IsDoctorType(me.Type)) return HealthArticleReviewResult.Fail(403, "Only doctors can review.");

            var decision = decisionInput?.Trim().ToUpperInvariant();
            if (decision != ReviewContributorArticleRequestModel.Approve && decision != ReviewContributorArticleRequestModel.RequestChanges)
                return HealthArticleReviewResult.Fail(400, "decision must be APPROVE or REQUEST_CHANGES.");

            var revision = await _context.HealthArticleRevisions.FirstOrDefaultAsync(
                r => r.ArticleId == article.ArticleId && (r.Status == HealthArticleRevision.StatusDraft || r.Status == HealthArticleRevision.StatusInReview), ct);
            var reviewingEdit = article.Status == HealthArticle.StatusPublished && revision?.Status == HealthArticleRevision.StatusInReview;
            var reviewingArticle = article.Status == HealthArticle.StatusInReview && article.ApprovedAt == null;
            if (!reviewingEdit && !reviewingArticle)
                return HealthArticleReviewResult.Fail(409, "This article is not waiting for your review.");

            var now = DateTime.UtcNow;
            string? comment = null;
            string outcome;

            if (decision == ReviewContributorArticleRequestModel.RequestChanges)
            {
                comment = commentInput?.Trim();
                if (comment == null || comment.Length < HealthArticleRules.MinChangeCommentLength)
                    return HealthArticleReviewResult.Fail(400, "Add a short comment so the writer knows what to change.");
                if (comment.Length > HealthArticleRules.MaxReasonLength)
                    return HealthArticleReviewResult.Fail(400, $"The comment is too long (max {HealthArticleRules.MaxReasonLength} characters).");

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
                outcome = HealthArticleReviewResult.OutcomeSentBack;
            }
            else
            {
                if (!accuracyConfirmed)
                    return HealthArticleReviewResult.Fail(400, "Confirm that you have checked the article for medical accuracy.");

                if (reviewingEdit)
                {
                    if (me.Status != HealthWikiContributor.StatusVerified)
                        return HealthArticleReviewResult.Fail(403, "You can approve edits to a live article after the team has verified your registration.");
                    HealthArticlePublisher.ApplyRevision(_context, article, revision!, HealthWikiAudit.ActorContributor, me.FullName, now);
                    article.ApprovedAt = now;
                    outcome = HealthArticleReviewResult.OutcomePublished;
                }
                else
                {
                    article.ApprovedAt = now;
                    article.ApprovedByName = me.FullName;
                    // Goes live now when the doctor is verified; otherwise it waits and the verification publishes it.
                    HealthArticlePublisher.TryPublish(_context, article, me, me.FullName, now);
                    article.UpdatedAt = now;
                    outcome = article.Status == HealthArticle.StatusPublished
                        ? HealthArticleReviewResult.OutcomePublished
                        : HealthArticleReviewResult.OutcomeAwaitingVerification;
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
                return HealthArticleReviewResult.Fail(409, "The article was changed at the same moment. Reload it and try again.");
            }

            return new HealthArticleReviewResult { Success = true, StatusCode = 200, Outcome = outcome, Article = article, ReviewerName = me.FullName };
        }
    }
}
